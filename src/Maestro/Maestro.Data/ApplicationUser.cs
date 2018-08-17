// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Maestro.Data
{
    public class ApplicationUser : IdentityUser<int>
    {
        public List<ApplicationUserPersonalAccessToken> PersonalAccessTokens { get; set; }

        [PersonalData]
        public string FullName { get; set; }

        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    }
}
