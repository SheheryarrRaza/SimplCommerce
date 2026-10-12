using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Payments.Models;

namespace SimplCommerce.Module.PaymentMollie.Data
{
    public class PaymentMollieCustomModelBuilder : ICustomModelBuilder
    {
        public void Build(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PaymentProvider>().HasData(
                new PaymentProvider("MolliePayment") { Name = "Mollie", LandingViewComponentName = "MollieLanding", ConfigureUrl = "payments-mollie-config", IsEnabled = false, AdditionalSettings = "{}" }
            );
        }
    }
}
