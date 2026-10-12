using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure.Web;
using SimplCommerce.Module.Checkouts.Services;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Services;
using SimplCommerce.Module.Orders.Models;
using SimplCommerce.Module.Orders.Services;
using SimplCommerce.Module.PaymentMollie.Models;
using SimplCommerce.Module.PaymentMollie.Services;
using SimplCommerce.Module.PaymentMollie.ViewModels;
using SimplCommerce.Module.Payments.Models;

namespace SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Area("PaymentMollie")]
    public class MolliePaymentController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IRepository<Order> _orderRepository;
        private readonly IRepository<Payment> _paymentRepository;
        private readonly IWorkContext _workContext;
        private readonly ICheckoutService _checkoutService;
        private readonly IRepositoryWithTypedId<PaymentProvider, string> _paymentProviderRepository;
        private readonly IMollieClient _mollieClient;
        private readonly ICurrencyService _currencyService;
        private readonly Lazy<MollieConfigForm> _setting;

        public MolliePaymentController(
            IOrderService orderService,
            IRepository<Order> orderRepository,
            IRepository<Payment> paymentRepository,
            ICheckoutService checkoutService,
            IWorkContext workContext,
            IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepository,
            IMollieClient mollieClient,
            ICurrencyService currencyService)
        {
            _orderService = orderService;
            _orderRepository = orderRepository;
            _paymentRepository = paymentRepository;
            _checkoutService = checkoutService;
            _workContext = workContext;
            _paymentProviderRepository = paymentProviderRepository;
            _mollieClient = mollieClient;
            _currencyService = currencyService;
            _setting = new Lazy<MollieConfigForm>(GetSetting);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MollieCheckout(Guid checkoutId)
        {
            var cart = await _checkoutService.GetCheckoutDetails(checkoutId);
            if (cart == null)
            {
                return NotFound();
            }

            var orderCreateResult = await _orderService.CreateOrder(checkoutId, PaymentProviderHelper.MolliePaymentProviderId, 0, OrderStatus.PendingPayment);
            if (!orderCreateResult.Success)
            {
                TempData["Error"] = orderCreateResult.Error;
                return Redirect("~/checkout/payment");
            }

            var order = orderCreateResult.Value;
            var rootUrl = Request.GetFullHostingUrlRoot();
            var currency = new RegionInfo(_currencyService.CurrencyCulture.LCID).ISOCurrencySymbol;
            var metadata = new Dictionary<string, string> { { "orderId", order.Id.ToString(CultureInfo.InvariantCulture) } };

            var molliePayment = await _mollieClient.CreatePayment(
                apiKey: _setting.Value.ApiKey,
                amount: order.OrderTotal,
                currency: currency,
                description: $"Order #{order.Id}",
                redirectUrl: $"{rootUrl}/mollie/return?orderId={order.Id}",
                webhookUrl: $"{rootUrl}/mollie/webhook",
                metadata: metadata);

            if (molliePayment?.Links?.Checkout?.Href == null)
            {
                TempData["Error"] = "Could not start the Mollie payment.";
                return Redirect($"~/checkout/error?orderId={order.Id}");
            }

            var payment = new Payment
            {
                OrderId = order.Id,
                PaymentFee = order.PaymentFeeAmount,
                Amount = order.OrderTotal,
                PaymentMethod = PaymentProviderHelper.MolliePaymentProviderId,
                Status = PaymentStatus.Pending,
                GatewayTransactionId = molliePayment.Id,
                CreatedOn = DateTimeOffset.UtcNow,
            };

            _paymentRepository.Add(payment);
            await _paymentRepository.SaveChangesAsync();

            return Redirect(molliePayment.Links.Checkout.Href);
        }

        /// <summary>
        /// The customer's browser is redirected here by Mollie after they finish on the hosted
        /// checkout page. The order is only confirmed as paid after calling back into Mollie's API
        /// with the stored API key; the rest of the query string (if any) is never trusted.
        /// </summary>
        [HttpGet("mollie/return")]
        [Authorize]
        public async Task<IActionResult> Return(long orderId)
        {
            var order = _orderRepository.Query().FirstOrDefault(x => x.Id == orderId);
            if (order == null)
            {
                return NotFound();
            }

            var currentUser = await _workContext.GetCurrentUser();
            if (currentUser == null || order.CreatedById != currentUser.Id)
            {
                return Forbid();
            }

            var payment = GetLatestPayment(orderId);
            if (payment == null)
            {
                return NotFound();
            }

            var isPaid = await ConfirmPaymentIfPending(order, payment);

            return isPaid
                ? Redirect($"~/checkout/success?orderId={orderId}")
                : Redirect($"~/checkout/error?orderId={orderId}");
        }

        /// <summary>
        /// Called directly by Mollie's servers, so it cannot be authenticated with the customer's
        /// cookie. The `id` from the body is only ever used as a lookup key: the authoritative status
        /// always comes from a server-to-server call to Mollie's API using our own stored API key, so
        /// a forged webhook call cannot mark an order as paid by itself.
        /// </summary>
        [HttpPost("mollie/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromForm] string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var payment = _paymentRepository.Query().FirstOrDefault(x => x.GatewayTransactionId == id && x.PaymentMethod == PaymentProviderHelper.MolliePaymentProviderId);
            if (payment == null)
            {
                return NotFound();
            }

            var order = _orderRepository.Query().FirstOrDefault(x => x.Id == payment.OrderId);
            if (order == null)
            {
                return NotFound();
            }

            await ConfirmPaymentIfPending(order, payment);

            return Accepted();
        }

        /// <summary>
        /// Confirms the payment against Mollie's API and records the outcome, unless it was already
        /// settled (by the browser return or the webhook, whichever ran first), in which case nothing
        /// is changed and no duplicate Payment row is created.
        /// </summary>
        private async Task<bool> ConfirmPaymentIfPending(Order order, Payment payment)
        {
            if (payment.Status != PaymentStatus.Pending)
            {
                return payment.Status == PaymentStatus.Succeeded;
            }

            var molliePayment = await _mollieClient.GetPayment(_setting.Value.ApiKey, payment.GatewayTransactionId);
            var isPaid = string.Equals(molliePayment?.Status, "paid", StringComparison.OrdinalIgnoreCase);

            payment.Status = isPaid ? PaymentStatus.Succeeded : PaymentStatus.Failed;
            payment.LatestUpdatedOn = DateTimeOffset.UtcNow;
            if (!isPaid)
            {
                payment.FailureMessage = $"Mollie payment status: {molliePayment?.Status}";
            }

            order.OrderStatus = isPaid ? OrderStatus.PaymentReceived : OrderStatus.PaymentFailed;

            await _paymentRepository.SaveChangesAsync();

            return isPaid;
        }

        private Payment GetLatestPayment(long orderId)
        {
            return _paymentRepository.Query()
                .Where(x => x.OrderId == orderId && x.PaymentMethod == PaymentProviderHelper.MolliePaymentProviderId)
                .OrderByDescending(x => x.CreatedOn)
                .FirstOrDefault();
        }

        private MollieConfigForm GetSetting()
        {
            var mollieProvider = _paymentProviderRepository.Query().FirstOrDefault(x => x.Id == PaymentProviderHelper.MolliePaymentProviderId);
            return JsonConvert.DeserializeObject<MollieConfigForm>(mollieProvider.AdditionalSettings);
        }
    }
}
