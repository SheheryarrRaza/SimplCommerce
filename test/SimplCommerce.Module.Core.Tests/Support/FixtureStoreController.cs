using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Models;

namespace SimplCommerce.Module.Core.Tests.Support
{
    [Authorize]
    [Route("fixtures/store")]
    public class FixtureStoreController : Controller
    {
        private readonly IRepository<UserAddress> _addressRepository;
        private readonly IRepository<User> _userRepository;

        public FixtureStoreController(IRepository<UserAddress> addressRepository, IRepository<User> userRepository)
        {
            _addressRepository = addressRepository;
            _userRepository = userRepository;
        }

        [HttpGet("addresses/{id}")]
        public async Task<IActionResult> GetAddress(long id)
        {
            var address = await _addressRepository.Query().Include(x => x.Address).FirstOrDefaultAsync(x => x.Id == id);
            if (address == null)
            {
                return NotFound();
            }

            return Ok(new { address.Id, address.Address.ContactName, address.Address.Phone, address.Address.AddressLine1 });
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(long id, [FromBody] User model)
        {
            var user = await _userRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.VendorId = model.VendorId;
            user.IsDeleted = model.IsDeleted;
            await _userRepository.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("exports")]
        public IActionResult DownloadExport(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "exports", name);
            return PhysicalFile(path, "text/csv", name);
        }

        [HttpGet("switch")]
        public IActionResult Switch(string returnUrl)
        {
            return Redirect(returnUrl);
        }

        [HttpPost("theme")]
        public IActionResult SetTheme(string theme)
        {
            Response.Cookies.Append("fixture-theme", theme, new CookieOptions { HttpOnly = false, Expires = DateTimeOffset.UtcNow.AddDays(30) });
            return NoContent();
        }

        [HttpPost("imports")]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> Import(IFormFile file)
        {
            var target = Path.Combine(AppContext.BaseDirectory, "imports", Path.GetFileName(file.FileName));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (var stream = new FileStream(target, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { file.FileName, file.Length });
        }
    }
}
