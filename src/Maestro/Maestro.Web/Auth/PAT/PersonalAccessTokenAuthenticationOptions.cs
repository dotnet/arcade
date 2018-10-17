// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authentication;

namespace Maestro.Web
{
    public class PersonalAccessTokenAuthenticationOptions<TUser> : AuthenticationSchemeOptions
    {
        public new PersonalAccessTokenEvents<TUser> Events
        {
            get => (PersonalAccessTokenEvents<TUser>) base.Events;
            set => base.Events = value;
        }

        public int PasswordSize { get; set; } = 16;

        public string TokenName { get; set; } = "Bearer";
    }
}
