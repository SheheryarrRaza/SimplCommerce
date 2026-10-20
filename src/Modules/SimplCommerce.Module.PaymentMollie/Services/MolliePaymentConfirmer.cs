using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Orders.Models;
using SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.ViewModels;
using SimplCommerce.Module.PaymentMollie.Models;
using SimplCommerce.Module.Payments.Models;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    /// <summary>
    /// The only place where a Mollie payment can change an order. It asks Mollie for the
    /// payment status with the stored API key and never trusts values sent by the customer.
    /// </summary>
    public class MolliePaymentConfirmer
    {
        private readonly IMollieClient m_mollieClient;
        private readonly IRepository<Payment> m_paymentRepository;
        private readonly IRepository<Order> m_orderRepository;
        private readonly IRepositoryWithTypedId<PaymentProvider, string> m_paymentProviderRepository;
        private readonly ILogger<MolliePaymentConfirmer> m_logger;

        public MolliePaymentConfirmer(
            IMollieClient mollieClient,
            IRepository<Payment> paymentRepository,
            IRepository<Order> orderRepository,
            IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepository,
            ILogger<MolliePaymentConfirmer> logger)
        {
            m_mollieClient = mollieClient;
            m_paymentRepository = paymentRepository;
            m_orderRepository = orderRepository;
            m_paymentProviderRepository = paymentProviderRepository;
            m_logger = logger;
        }

        /// <summary>
        /// Checks the Mollie payment with the given id and, if it is still pending, finalises the
        /// matching Payment and Order. Returns the Payment, or null when no Mollie payment with that id was created here.
        /// Throws <see cref="MollieException"/> when Mollie cannot be reached or rejects the call.
        /// </summary>
        public async Task<Payment> ConfirmAsync(string molliePaymentId)
        {
            if (!MollieHelper.IsValidPaymentId(molliePaymentId))
            {
                return null;
            }

            var payment = await m_paymentRepository.Query()
                .FirstOrDefaultAsync(x => x.GatewayTransactionId == molliePaymentId && x.PaymentMethod == PaymentProviderHelper.MollieProviderId);
            if (payment == null)
            {
                return null;
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                return payment;
            }

            var order = await m_orderRepository.Query().FirstOrDefaultAsync(x => x.Id == payment.OrderId);
            if (order == null)
            {
                return null;
            }

            if (order.OrderStatus != OrderStatus.PendingPayment)
            {
                m_logger.LogWarning("Mollie payment {PaymentId} was not applied because order {OrderId} is no longer pending payment.", molliePaymentId, order.Id);
                return payment;
            }

            var apiKey = await GetApiKeyAsync();
            var info = await m_mollieClient.GetPaymentAsync(apiKey, molliePaymentId);

            switch (MollieHelper.MapStatus(info.Status))
            {
                case MollieStatus.Paid:
                    if (info.Amount != Math.Round(order.OrderTotal, 2, MidpointRounding.AwayFromZero))
                    {
                        m_logger.LogWarning("Mollie payment {PaymentId} is paid but its amount does not match order {OrderId}.", molliePaymentId, order.Id);
                        return payment;
                    }

                    payment.Status = PaymentStatus.Succeeded;
                    order.OrderStatus = OrderStatus.PaymentReceived;
                    break;
                case MollieStatus.Failed:
                    payment.Status = PaymentStatus.Failed;
                    payment.FailureMessage = $"Mollie payment {info.Status}";
                    order.OrderStatus = OrderStatus.PaymentFailed;
                    break;
                default:
                    return payment;
            }

            payment.LatestUpdatedOn = DateTimeOffset.UtcNow;
            order.LatestUpdatedOn = DateTimeOffset.UtcNow;
            await m_paymentRepository.SaveChangesAsync();
            return payment;
        }

        public async Task<string> GetApiKeyAsync()
        {
            var provider = await m_paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == PaymentProviderHelper.MollieProviderId);
            return ReadApiKey(provider?.AdditionalSettings);
        }

        public static string ReadApiKey(string additionalSettings)
        {
            if (string.IsNullOrWhiteSpace(additionalSettings))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<MollieConfigForm>(additionalSettings)?.ApiKey;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
