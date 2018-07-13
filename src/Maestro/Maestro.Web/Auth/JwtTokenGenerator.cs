using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Maestro.Web
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly IHttpContextAccessor _context;
        private readonly IOptionsMonitor<JwtBearerOptions> _options;

        public JwtTokenGenerator(IHttpContextAccessor context, IOptionsMonitor<JwtBearerOptions> options)
        {
            _context = context;
            _options = options;
        }

        public string Generate()
        {
            var options = _options.Get(JwtBearerDefaults.AuthenticationScheme);
            string issuer = options.TokenValidationParameters.ValidIssuer;
            string aud = options.TokenValidationParameters.ValidAudience;
            SecurityKey key = options.TokenValidationParameters.IssuerSigningKey;
            ClaimsPrincipal user = _context.HttpContext.User;
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = handler.CreateJwtSecurityToken(
                issuer: issuer,
                audience: aud,
                subject: new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }),
                expires: DateTime.UtcNow.AddYears(1),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );
            return handler.WriteToken(token);
        }

        public string TryGenerate()
        {
            if (_context.HttpContext.User.Identity.Name != null)
            {
                return Generate();
            }
            return null;
        }
    }
}
