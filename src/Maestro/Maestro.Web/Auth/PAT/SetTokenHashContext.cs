// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Http;

namespace Maestro.Web
{
    public class SetTokenHashContext<TUser>
    {
        public SetTokenHashContext(HttpContext httpContext, TUser user, string name, string hash)
        {
            HttpContext = httpContext;
            User = user;
            Name = name;
            Hash = hash;
        }

        public HttpContext HttpContext { get; }

        public TUser User { get; }
        public string Name { get; }
        public string Hash { get; }
    }
}
