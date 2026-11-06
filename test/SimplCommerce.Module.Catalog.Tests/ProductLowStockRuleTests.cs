using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SimplCommerce.Module.Catalog.Areas.Catalog.ViewModels;
using SimplCommerce.Module.Catalog.Models;
using Xunit;

namespace SimplCommerce.Module.Catalog.Tests
{
    public class ProductLowStockRuleTests
    {
        [Theory]
        [InlineData(5, 5, true, true)]
        [InlineData(6, 5, true, false)]
        [InlineData(0, null, true, false)]
        [InlineData(0, 5, false, false)]
        [InlineData(0, 0, true, true)]
        public void IsLowStock_ReturnsExpectedResult(int stock, int? threshold, bool trackingEnabled, bool expected)
        {
            var product = new Product
            {
                StockQuantity = stock,
                LowStockThreshold = threshold,
                StockTrackingIsEnabled = trackingEnabled
            };

            var isLowStock = ProductLowStockRule.IsLowStock.Compile()(product);

            Assert.Equal(expected, isLowStock);
        }

        [Fact]
        public void Clone_CopiesLowStockThreshold()
        {
            var product = new Product { LowStockThreshold = 5 };

            var clone = product.Clone();

            Assert.Equal(5, clone.LowStockThreshold);
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, true)]
        [InlineData(null, true)]
        public void ProductVm_ValidatesLowStockThreshold(int? threshold, bool expectedValid)
        {
            var model = new ProductVm { Name = "Sample product", Slug = "sample-product", LowStockThreshold = threshold };
            var validationResults = new List<ValidationResult>();

            var isValid = Validator.TryValidateObject(model, new ValidationContext(model), validationResults, true);

            Assert.Equal(expectedValid, isValid);
            if (!expectedValid)
            {
                Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(ProductVm.LowStockThreshold)));
            }
        }
    }
}
