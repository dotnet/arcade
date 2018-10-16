// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Maestro.Web
{
    public class
        PersonalAccessTokenValidatePrincipalContext<TUser> : PrincipalContext<
            PersonalAccessTokenAuthenticationOptions<TUser>>
    {
        public PersonalAccessTokenValidatePrincipalContext(
            HttpContext context,
            AuthenticationScheme scheme,
            PersonalAccessTokenAuthenticationOptions<TUser> options,
            AuthenticationTicket ticket,
            TUser user) : base(context, scheme, options, ticket?.Properties)
        {
            Context = context;
            User = user;
            Principal = ticket?.Principal;
        }

        public HttpContext Context { get; }
        public TUser User { get; }

        /// <summary>
        ///     Called to replace the claims principal. The supplied principal will replace the value of the
        ///     Principal property, which determines the identity of the authenticated request.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal" /> used as the replacement</param>
        public void ReplacePrincipal(ClaimsPrincipal principal)
        {
            Principal = principal;
        }

        /// <summary>
        ///     Called to reject the incoming principal. This may be done if the application has determined the
        ///     account is no longer active, and the request should be treated as if it was anonymous.
        /// </summary>
        public void RejectPrincipal()
        {
            Principal = null;
        }
    }
}
