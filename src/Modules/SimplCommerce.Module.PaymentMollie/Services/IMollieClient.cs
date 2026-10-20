using System.Threading.Tasks;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    public interface IMollieClient
    {
        /// <param name="amount">Amount already formatted for Mollie, see <see cref="MollieHelper.FormatAmount"/>.</param>
        Task<MolliePaymentCreated> CreatePaymentAsync(string apiKey, string amount, string currency, string description, string redirectUrl, string webhookUrl, long orderId);

        Task<MolliePaymentInfo> GetPaymentAsync(string apiKey, string paymentId);
    }

    public class MolliePaymentCreated
    {
        public string Id { get; set; }

        public string CheckoutUrl { get; set; }
    }

    public class MolliePaymentInfo
    {
        public string Id { get; set; }

        public string Status { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; }
    }
}
