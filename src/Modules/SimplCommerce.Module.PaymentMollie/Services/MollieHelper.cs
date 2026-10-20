using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    public enum MollieStatus
    {
        Open,
        Paid,
        Failed
    }

    public static class MollieHelper
    {
        private static readonly Regex PaymentIdPattern = new Regex("^tr_[A-Za-z0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public static string FormatAmount(decimal amount, bool zeroDecimal)
        {
            return amount.ToString(zeroDecimal ? "F0" : "F2", CultureInfo.InvariantCulture);
        }

        public static bool IsValidPaymentId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.Length <= 100 && PaymentIdPattern.IsMatch(id);
        }

        public static bool IsTrustedCheckoutUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            return uri.Host.Equals("mollie.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".mollie.com", StringComparison.OrdinalIgnoreCase);
        }

        public static MollieStatus MapStatus(string status)
        {
            switch (status)
            {
                case "paid":
                    return MollieStatus.Paid;
                case "failed":
                case "canceled":
                case "expired":
                    return MollieStatus.Failed;
                default:
                    return MollieStatus.Open;
            }
        }
    }
}
