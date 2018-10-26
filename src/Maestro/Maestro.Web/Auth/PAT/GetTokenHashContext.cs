// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Http;

namespace Maestro.Web
{
    public class GetTokenHashContext<TUser>
    {
        public GetTokenHashContext(HttpContext httpContext, int tokenId)
        {
            HttpContext = httpContext;
            TokenId = tokenId;
            Succeeded = false;
        }

        public HttpContext HttpContext { get; }
        public int TokenId { get; }

        public string Hash { get; private set; }
        public TUser User { get; private set; }
        public bool Succeeded { get; private set; }

        public void Success(string hash, TUser user)
        {
            Hash = hash;
            User = user;
            Succeeded = true;
        }
    }
}
