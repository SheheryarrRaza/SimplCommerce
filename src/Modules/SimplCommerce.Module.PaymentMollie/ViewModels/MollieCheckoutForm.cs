using System;

namespace SimplCommerce.Module.PaymentMollie.ViewModels
{
    public class MollieCheckoutForm
    {
        public decimal PaymentFee { get; set; }

        public Guid CheckoutId { get; set; }
    }
}
