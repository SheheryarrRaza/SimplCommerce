using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Models;

namespace SimplCommerce.Module.Core.Areas.Core.Controllers
{
    [Area("Core")]
    [Route("api/device-session")]
    public class DeviceSessionApiController : Controller
    {
        private readonly IRepository<User> _userRepository;
        private readonly IWorkContext _workContext;
        private readonly IConfiguration _configuration;

        public DeviceSessionApiController(IRepository<User> userRepository, IWorkContext workContext, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _workContext = workContext;
            _configuration = configuration;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create()
        {
            var user = await _workContext.GetCurrentUser();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["DeviceSession:SigningKey"]));
            var token = new JwtSecurityToken(
                issuer: _configuration["DeviceSession:Issuer"],
                claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()) },
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }

        [AllowAnonymous]
        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var header = Request.Headers["Authorization"].ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            var token = new JwtSecurityTokenHandler().ReadJwtToken(header.Substring("Bearer ".Length));
            if (token.Issuer != _configuration["DeviceSession:Issuer"] || token.ValidTo < DateTime.UtcNow)
            {
                return Unauthorized();
            }

            var userId = long.Parse(token.Subject);
            var user = await _userRepository.Query().FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new { user.Id, user.FullName, user.Email, user.PhoneNumber });
        }
    }
}
