using System;
using System.IdentityModel.Tokens.Jwt;

namespace SimplCommerce.Module.Core.Tests.Support
{
    public class FixtureTokenReader
    {
        public long? ReadUserId(string authorizationHeader)
        {
            if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeader.Substring("Bearer ".Length));
            if (token.ValidTo < DateTime.UtcNow)
            {
                return null;
            }

            return long.Parse(token.Subject);
        }
    }
}
