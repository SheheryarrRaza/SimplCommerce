using System;
using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure.Web;
using SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.ViewModels;

namespace SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Components
{
    public class MollieLandingViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(Guid checkoutId)
        {
            var model = new MollieCheckoutForm { CheckoutId = checkoutId };
            return View(this.GetViewPath(), model);
        }
    }
}
