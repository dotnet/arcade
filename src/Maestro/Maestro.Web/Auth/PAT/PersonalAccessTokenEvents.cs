// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Maestro.Web
{
    public class PersonalAccessTokenEvents<TUser>
    {
        public delegate Task<(string tokenHash, TUser user)?> GetTokenHashCallback(HttpContext context, int tokenId);

        public delegate Task<int> NewTokenCallback(HttpContext context, TUser user, string name, string hash);

        public NewTokenCallback NewToken { get; set; }
        public GetTokenHashCallback GetTokenHash { get; set; }
    }
}
