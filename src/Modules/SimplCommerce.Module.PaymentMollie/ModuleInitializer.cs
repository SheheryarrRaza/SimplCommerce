using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SimplCommerce.Infrastructure;
using SimplCommerce.Infrastructure.Modules;
using SimplCommerce.Module.PaymentMollie.Services;

namespace SimplCommerce.Module.PaymentMollie
{
    public class ModuleInitializer : IModuleInitializer
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            GlobalConfiguration.RegisterAngularModule("simplAdmin.paymentMollie");
            serviceCollection.AddScoped<IMollieClient, MollieClient>();
            serviceCollection.AddScoped<MolliePaymentConfirmer>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
        }
    }
}
