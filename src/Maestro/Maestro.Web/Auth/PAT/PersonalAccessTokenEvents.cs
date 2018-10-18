// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Maestro.Web
{
    public class PersonalAccessTokenEvents<TUser>
    {
        public Func<SetTokenHashContext<TUser>, Task<int>> OnSetTokenHash { get; set; } = context =>
            throw new NotImplementedException("An implementation of SetTokenHash must be provided.");

        public Func<GetTokenHashContext<TUser>, Task> OnGetTokenHash { get; set; } = context =>
            throw new NotImplementedException("An implementation of GetTokenHash must be provided.");

        public Func<PersonalAccessTokenValidatePrincipalContext<TUser>, Task> OnValidatePrincipal { get; set; } =
            context => Task.CompletedTask;

        public virtual Task<int> SetTokenHash(SetTokenHashContext<TUser> context)
        {
            return OnSetTokenHash(context);
        }

        public virtual Task GetTokenHash(GetTokenHashContext<TUser> context)
        {
            return OnGetTokenHash(context);
        }

        public virtual Task ValidatePrincipal(PersonalAccessTokenValidatePrincipalContext<TUser> context)
        {
            return OnValidatePrincipal(context);
        }
    }
}
