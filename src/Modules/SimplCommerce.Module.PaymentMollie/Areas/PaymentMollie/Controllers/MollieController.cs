using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure.Helpers;
using SimplCommerce.Infrastructure.Web;
using SimplCommerce.Module.Checkouts.Models;
using SimplCommerce.Module.Checkouts.Services;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Services;
using SimplCommerce.Module.Orders.Models;
using SimplCommerce.Module.Orders.Services;
using SimplCommerce.Module.PaymentMollie.Models;
using SimplCommerce.Module.PaymentMollie.Services;
using SimplCommerce.Module.Payments.Models;

namespace SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Controllers
{
    [Area("PaymentMollie")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MollieController : Controller
    {
        private const string GenericError = "The payment with Mollie could not be started. Please try again or choose another payment method.";

        private readonly IRepositoryWithTypedId<Checkout, Guid> m_checkoutRepository;
        private readonly IOrderService m_orderService;
        private readonly IRepository<Order> m_orderRepository;
        private readonly IRepository<Payment> m_paymentRepository;
        private readonly IRepositoryWithTypedId<PaymentProvider, string> m_paymentProviderRepository;
        private readonly IWorkContext m_workContext;
        private readonly ICurrencyService m_currencyService;
        private readonly IMollieClient m_mollieClient;
        private readonly MolliePaymentConfirmer m_confirmer;
        private readonly ILogger<MollieController> m_logger;

        public MollieController(
            IRepositoryWithTypedId<Checkout, Guid> checkoutRepository,
            IOrderService orderService,
            IRepository<Order> orderRepository,
            IRepository<Payment> paymentRepository,
            IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepository,
            IWorkContext workContext,
            ICurrencyService currencyService,
            IMollieClient mollieClient,
            MolliePaymentConfirmer confirmer,
            ILogger<MollieController> logger)
        {
            m_checkoutRepository = checkoutRepository;
            m_orderService = orderService;
            m_orderRepository = orderRepository;
            m_paymentRepository = paymentRepository;
            m_paymentProviderRepository = paymentProviderRepository;
            m_workContext = workContext;
            m_currencyService = currencyService;
            m_mollieClient = mollieClient;
            m_confirmer = confirmer;
            m_logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Guid checkoutId)
        {
            var currentUser = await m_workContext.GetCurrentUser();
            var checkout = await m_checkoutRepository.Query().FirstOrDefaultAsync(x => x.Id == checkoutId);
            if (checkout == null)
            {
                return NotFound();
            }

            if (checkout.CustomerId != currentUser.Id)
            {
                return Forbid();
            }

            var provider = await m_paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == PaymentProviderHelper.MollieProviderId);
            var apiKey = MolliePaymentConfirmer.ReadApiKey(provider?.AdditionalSettings);
            if (provider == null || !provider.IsEnabled || string.IsNullOrWhiteSpace(apiKey))
            {
                TempData["Error"] = GenericError;
                return Redirect("~/checkout/payment");
            }

            var orderCreationResult = await m_orderService.CreateOrder(checkoutId, PaymentProviderHelper.MollieProviderId, 0, OrderStatus.PendingPayment);
            if (!orderCreationResult.Success)
            {
                TempData["Error"] = orderCreationResult.Error;
                return Redirect("~/checkout/payment");
            }

            var order = orderCreationResult.Value;
            var zeroDecimal = CurrencyHelper.IsZeroDecimalCurrencies(m_currencyService.CurrencyCulture);
            var regionInfo = new RegionInfo(m_currencyService.CurrencyCulture.LCID);
            var rootUrl = Request.GetFullHostingUrlRoot();
            var payment = new Payment
            {
                OrderId = order.Id,
                Amount = order.OrderTotal,
                PaymentMethod = PaymentProviderHelper.MollieProviderId,
                CreatedOn = DateTimeOffset.UtcNow,
                Status = PaymentStatus.Pending
            };

            try
            {
                var created = await m_mollieClient.CreatePaymentAsync(
                    apiKey,
                    MollieHelper.FormatAmount(order.OrderTotal, zeroDecimal),
                    regionInfo.ISOCurrencySymbol,
                    $"Order {order.Id}",
                    $"{rootUrl}/mollie/return?orderId={order.Id}",
                    $"{rootUrl}/mollie/webhook",
                    order.Id);

                if (!MollieHelper.IsValidPaymentId(created.Id) || !MollieHelper.IsTrustedCheckoutUrl(created.CheckoutUrl))
                {
                    throw new MollieException("Mollie returned an unexpected response.");
                }

                payment.GatewayTransactionId = created.Id;
                m_paymentRepository.Add(payment);
                await m_paymentRepository.SaveChangesAsync();
                return Redirect(created.CheckoutUrl);
            }
            catch (MollieException ex)
            {
                m_logger.LogWarning("Could not create a Mollie payment for order {OrderId}: {Reason}", order.Id, ex.Message);
                payment.Status = PaymentStatus.Failed;
                payment.FailureMessage = ex.Message;
                order.OrderStatus = OrderStatus.PaymentFailed;
                m_paymentRepository.Add(payment);
                await m_paymentRepository.SaveChangesAsync();
                TempData["Error"] = GenericError;
                return Redirect($"~/checkout/error?orderId={order.Id}");
            }
        }

        [HttpGet("mollie/return")]
        public async Task<IActionResult> Return(long orderId)
        {
            var currentUser = await m_workContext.GetCurrentUser();
            var order = await m_orderRepository.Query().FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null)
            {
                return NotFound();
            }

            if (order.CustomerId != currentUser.Id)
            {
                return Forbid();
            }

            var payment = await m_paymentRepository.Query()
                .Where(x => x.OrderId == order.Id && x.PaymentMethod == PaymentProviderHelper.MollieProviderId && x.GatewayTransactionId != null)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (payment == null)
            {
                return NotFound();
            }

            // Nothing in the query string decides the outcome: the status comes from Mollie.
            try
            {
                payment = await m_confirmer.ConfirmAsync(payment.GatewayTransactionId) ?? payment;
            }
            catch (MollieException ex)
            {
                m_logger.LogWarning("Could not confirm Mollie payment for order {OrderId}: {Reason}", order.Id, ex.Message);
                TempData["Error"] = "The payment with Mollie could not be confirmed yet. If you completed it, the order will update once Mollie confirms it.";
                return Redirect($"~/checkout/error?orderId={order.Id}");
            }

            if (payment.Status == PaymentStatus.Failed)
            {
                TempData["Error"] = "The payment with Mollie was not completed.";
                return Redirect($"~/checkout/error?orderId={order.Id}");
            }

            return Redirect($"~/checkout/success?orderId={order.Id}");
        }

        [AllowAnonymous]
        [HttpPost("mollie/webhook")]
        [RequestSizeLimit(1024)]
        [MollieWebhookThrottle]
        public async Task<IActionResult> Webhook([FromForm] string id)
        {
            if (!MollieHelper.IsValidPaymentId(id))
            {
                return BadRequest();
            }

            try
            {
                await m_confirmer.ConfirmAsync(id);
                return Ok();
            }
            catch (MollieException ex)
            {
                m_logger.LogWarning("Could not confirm Mollie payment {PaymentId}: {Reason}", id, ex.Message);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }
    }
}
