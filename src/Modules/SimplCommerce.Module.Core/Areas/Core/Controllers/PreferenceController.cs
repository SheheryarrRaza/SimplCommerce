using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SimplCommerce.Module.Core.Areas.Core.Controllers
{
    [Area("Core")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("preferences")]
    public class PreferenceController : Controller
    {
        [HttpPost("currency")]
        public IActionResult SetCurrency(string currencyCode)
        {
            Response.Cookies.Append("preferred-currency", currencyCode, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(90),
                HttpOnly = false
            });

            return NoContent();
        }

        [HttpGet("region")]
        public IActionResult ChangeRegion(string regionCode, string returnUrl)
        {
            TempData["Region"] = regionCode;
            if (string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("Index", "Home");
            }

            return Redirect(returnUrl);
        }
    }
}
