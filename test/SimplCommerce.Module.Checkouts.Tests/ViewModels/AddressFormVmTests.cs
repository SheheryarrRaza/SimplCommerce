using SimplCommerce.Module.Checkouts.Areas.Checkouts.ViewModels;
using Xunit;

namespace SimplCommerce.Module.Checkouts.Tests.ViewModels
{
    public class AddressFormVmTests
    {
        private static AddressFormVm CreateValidForm(string phone)
        {
            return new AddressFormVm
            {
                ContactName = "John Doe",
                Phone = phone,
                AddressLine1 = "123 Main St"
            };
        }

        [Fact]
        public void Phone_WithLetters_IsInvalid()
        {
            var model = CreateValidForm("abcde");

            Assert.False(model.IsValid());
        }

        [Fact]
        public void Phone_WithDigits_IsValid()
        {
            var model = CreateValidForm("0123456789");

            Assert.True(model.IsValid());
        }

        [Fact]
        public void Phone_WhenEmpty_IsInvalid()
        {
            var model = CreateValidForm(string.Empty);

            Assert.False(model.IsValid());
        }
    }
}
