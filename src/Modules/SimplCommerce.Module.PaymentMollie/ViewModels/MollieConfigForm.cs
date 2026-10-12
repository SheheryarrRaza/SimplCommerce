using System.ComponentModel.DataAnnotations;

namespace SimplCommerce.Module.PaymentMollie.ViewModels
{
    public class MollieConfigForm
    {
        [Required(ErrorMessage = "The {0} field is required.")]
        public string ApiKey { get; set; }

        public decimal PaymentFee { get; set; }
    }
}
