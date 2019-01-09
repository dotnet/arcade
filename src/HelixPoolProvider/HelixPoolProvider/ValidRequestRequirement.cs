// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authorization;

namespace Microsoft.DotNet.HelixPoolProvider
{
    public class ValidRequestRequirement : IAuthorizationRequirement
    {
        private string _sharedSecret;

        public string SharedSecret { get => _sharedSecret; }

        public ValidRequestRequirement(string sharedSecret)
        {
            _sharedSecret = sharedSecret;
        }
    }
}
