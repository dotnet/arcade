// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Maestro.Web
{
    public class ContextAwareAuthenticationSchemeProvider : AuthenticationSchemeProvider
    {
        private readonly IHttpContextAccessor _contextAccessor;

        public ContextAwareAuthenticationSchemeProvider(
            IOptions<AuthenticationOptions> options,
            IHttpContextAccessor contextAccessor) : base(options)
        {
            _contextAccessor = contextAccessor;
        }

        public HttpContext Context => _contextAccessor.HttpContext;

        private Task<AuthenticationScheme> GetDefaultSchemeAsync()
        {
            if (Context.Request.Path.StartsWithSegments("/api"))
            {
                return GetSchemeAsync(PersonalAccessTokenDefaults.AuthenticationScheme);
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
