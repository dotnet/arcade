using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Maestro.Web
{
    public class ContextAwareAuthenticationSchemeProvider : AuthenticationSchemeProvider
    {
        private readonly IHttpContextAccessor _contextAccessor;
        public HttpContext Context => _contextAccessor.HttpContext;

        public ContextAwareAuthenticationSchemeProvider(IOptions<AuthenticationOptions> options, IHttpContextAccessor contextAccessor) : base(options)
        {
            _contextAccessor = contextAccessor;
        }

        private Task<AuthenticationScheme> GetDefaultSchemeAsync()
        {
            if (Context.Request.Path.StartsWithSegments("/api"))
            {
                return GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);
            }
            return GetSchemeAsync(IdentityConstants.ApplicationScheme);
        }

        public override Task<AuthenticationScheme> GetDefaultAuthenticateSchemeAsync()
        {
            return GetDefaultSchemeAsync();
        }

        public override Task<AuthenticationScheme> GetDefaultChallengeSchemeAsync()
        {
            return GetSchemeAsync(IdentityConstants.ExternalScheme);
        }

        public override Task<AuthenticationScheme> GetDefaultForbidSchemeAsync()
        {
            return GetDefaultSchemeAsync();
        }

        public override Task<AuthenticationScheme> GetDefaultSignInSchemeAsync()
        {
            return GetSchemeAsync(IdentityConstants.ApplicationScheme);
        }

        public override Task<AuthenticationScheme> GetDefaultSignOutSchemeAsync()
        {
            return GetSchemeAsync(IdentityConstants.ApplicationScheme);
        }
    }
}
