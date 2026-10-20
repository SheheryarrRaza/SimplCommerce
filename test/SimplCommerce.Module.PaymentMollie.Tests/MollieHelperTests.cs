using System.Globalization;
using SimplCommerce.Module.PaymentMollie.Services;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests
{
    public class MollieHelperTests
    {
        [Theory]
        [InlineData(10.5, false, "10.50")]
        [InlineData(1234.567, false, "1234.57")]
        [InlineData(1234.567, true, "1235")]
        public void FormatAmount_UsesDotAndFixedDecimals(double amount, bool zeroDecimal, string expected)
        {
            Assert.Equal(expected, MollieHelper.FormatAmount((decimal)amount, zeroDecimal));
        }

        [Fact]
        public void FormatAmount_IgnoresCurrentCulture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
                Assert.Equal("10.50", MollieHelper.FormatAmount(10.5m, false));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Theory]
        [InlineData("tr_abc123", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("../x", false)]
        [InlineData("tr_", false)]
        [InlineData("tr_a/b", false)]
        public void IsValidPaymentId_ChecksShape(string id, bool expected)
        {
            Assert.Equal(expected, MollieHelper.IsValidPaymentId(id));
        }

        [Theory]
        [InlineData("https://www.mollie.com/checkout/select-method/abc", true)]
        [InlineData("https://mollie.com/checkout", true)]
        [InlineData("http://www.mollie.com/checkout", false)]
        [InlineData("https://example.test/checkout", false)]
        [InlineData("https://mollie.com.evil.test/checkout", false)]
        [InlineData("https://evilmollie.com/checkout", false)]
        [InlineData("/checkout/abc", false)]
        [InlineData(null, false)]
        public void IsTrustedCheckoutUrl_OnlyAcceptsHttpsMollie(string url, bool expected)
        {
            Assert.Equal(expected, MollieHelper.IsTrustedCheckoutUrl(url));
        }

        [Theory]
        [InlineData("paid", MollieStatus.Paid)]
        [InlineData("failed", MollieStatus.Failed)]
        [InlineData("canceled", MollieStatus.Failed)]
        [InlineData("expired", MollieStatus.Failed)]
        [InlineData("open", MollieStatus.Open)]
        [InlineData("pending", MollieStatus.Open)]
        [InlineData("authorized", MollieStatus.Open)]
        [InlineData("something-new", MollieStatus.Open)]
        public void MapStatus_MapsMollieStatuses(string status, MollieStatus expected)
        {
            Assert.Equal(expected, MollieHelper.MapStatus(status));
        }
    }
}
