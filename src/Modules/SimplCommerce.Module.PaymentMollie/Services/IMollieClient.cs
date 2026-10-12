using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    /// <summary>
    /// Abstraction over the Mollie REST API (https://api.mollie.com/v2/payments) so controllers
    /// and tests don't depend on HttpClient directly.
    /// </summary>
    public interface IMollieClient
    {
        Task<MolliePaymentResponse> CreatePayment(string apiKey, decimal amount, string currency, string description, string redirectUrl, string webhookUrl, Dictionary<string, string> metadata);

        Task<MolliePaymentResponse> GetPayment(string apiKey, string paymentId);
    }
}
