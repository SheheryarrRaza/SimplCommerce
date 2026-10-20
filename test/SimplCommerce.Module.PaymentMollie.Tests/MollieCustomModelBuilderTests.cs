using System.Linq;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Module.PaymentMollie.Data;
using SimplCommerce.Module.Payments.Models;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests
{
    public class MollieCustomModelBuilderTests
    {
        [Fact]
        public void Build_SeedsOneDisabledProviderWithEmptySettings()
        {
            var modelBuilder = new ModelBuilder();
            var entity = modelBuilder.Entity<PaymentProvider>();
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name);
            entity.Property(x => x.IsEnabled);
            entity.Property(x => x.ConfigureUrl);
            entity.Property(x => x.LandingViewComponentName);
            entity.Property(x => x.AdditionalSettings);

            new PaymentMollieCustomModelBuilder().Build(modelBuilder);

            var seed = modelBuilder.Model.FindEntityType(typeof(PaymentProvider)).GetSeedData().ToList();
            var row = Assert.Single(seed);
            Assert.Equal("Mollie", row["Id"]);
            Assert.Equal("Mollie", row["Name"]);
            Assert.Equal("MollieLanding", row["LandingViewComponentName"]);
            Assert.Equal("payments-mollie-config", row["ConfigureUrl"]);
            Assert.Equal(false, row["IsEnabled"]);
            Assert.Equal("{}", row["AdditionalSettings"]);
        }
    }
}
