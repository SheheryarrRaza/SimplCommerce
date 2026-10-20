using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.ViewModels;
using SimplCommerce.Module.PaymentMollie.Models;
using SimplCommerce.Module.PaymentMollie.Services;
using SimplCommerce.Module.Payments.Models;

namespace SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Controllers
{
    [Area("PaymentMollie")]
    [Authorize(Roles = "admin")]
    [Route("api/mollie")]
    public class MollieApiController : Controller
    {
        private readonly IRepositoryWithTypedId<PaymentProvider, string> m_paymentProviderRepository;

        public MollieApiController(IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepository)
        {
            m_paymentProviderRepository = paymentProviderRepository;
        }

        [HttpGet("config")]
        public async Task<IActionResult> Config()
        {
            var mollieProvider = await m_paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == PaymentProviderHelper.MollieProviderId);
            if (mollieProvider == null)
            {
                return NotFound();
            }

            var model = new MollieConfigForm { ApiKey = MolliePaymentConfirmer.ReadApiKey(mollieProvider.AdditionalSettings) };
            return Ok(model);
        }

        [HttpPut("config")]
        public async Task<IActionResult> Config([FromBody] MollieConfigForm model)
        {
            if (model == null || !ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var mollieProvider = await m_paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == PaymentProviderHelper.MollieProviderId);
            if (mollieProvider == null)
            {
                return NotFound();
            }

            mollieProvider.AdditionalSettings = JsonConvert.SerializeObject(model);
            await m_paymentProviderRepository.SaveChangesAsync();
            return Accepted();
        }
    }
}
