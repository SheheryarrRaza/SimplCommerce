using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure.Web;
using SimplCommerce.Module.PaymentMollie.Models;
using SimplCommerce.Module.PaymentMollie.ViewModels;
using SimplCommerce.Module.Payments.Models;

namespace SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Components
{
    public class MollieLandingViewComponent : ViewComponent
    {
        private readonly IRepositoryWithTypedId<PaymentProvider, string> _paymentProviderRepository;

        public MollieLandingViewComponent(IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepository)
        {
            _paymentProviderRepository = paymentProviderRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync(Guid checkoutId)
        {
            var mollieProvider = await _paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == PaymentProviderHelper.MolliePaymentProviderId);
            var mollieSetting = JsonConvert.DeserializeObject<MollieConfigForm>(mollieProvider.AdditionalSettings);

            var model = new MollieCheckoutForm
            {
                PaymentFee = mollieSetting.PaymentFee,
                CheckoutId = checkoutId
            };

            return View(this.GetViewPath(), model);
        }
    }
}
