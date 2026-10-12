using System.Collections.Generic;
using Newtonsoft.Json;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    public class MollieAmount
    {
        public string Currency { get; set; }

        public string Value { get; set; }
    }

    public class MollieCreatePaymentRequest
    {
        public MollieAmount Amount { get; set; }

        public string Description { get; set; }

        public string RedirectUrl { get; set; }

        public string WebhookUrl { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
    }

    public class MollieCheckoutLink
    {
        public string Href { get; set; }
    }

    public class MolliePaymentLinks
    {
        public MollieCheckoutLink Checkout { get; set; }
    }

    /// <summary>
    /// Subset of the fields on a Mollie payment resource (https://docs.mollie.com/reference/v2/payments-api/get-payment)
    /// that this module needs to create a payment and confirm its outcome server-side.
    /// </summary>
    public class MolliePaymentResponse
    {
        public string Id { get; set; }

        /// <summary>
        /// One of: open, canceled, pending, authorized, expired, failed, paid.
        /// </summary>
        public string Status { get; set; }

        public MollieAmount Amount { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        [JsonProperty("_links")]
        public MolliePaymentLinks Links { get; set; }
    }
}
