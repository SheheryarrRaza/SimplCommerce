using System.ComponentModel.DataAnnotations;

namespace SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.ViewModels
{
    public class MollieConfigForm
    {
        [Required(ErrorMessage = "The {0} field is required.")]
        [StringLength(200)]
        [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "The {0} field may only contain letters, digits and underscores.")]
        public string ApiKey { get; set; }
    }
}
