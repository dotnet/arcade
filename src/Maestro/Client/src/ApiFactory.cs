// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Rest;

namespace Microsoft.DotNet.Maestro.Client
{
    public static class ApiFactory
    {
        public static IMaestroApi GetAuthenticated(string accessToken)
        {
            return new MaestroApi(new TokenCredentials(accessToken));
        }

        public static IMaestroApi GetAnonymous()
        {
            return new NoCredentialsMaestroApi();
        }

        public static IMaestroApi GetAuthenticated(string baseUri, string accessToken)
        {
            return new MaestroApi(new Uri(baseUri), new TokenCredentials(accessToken));
        }

        public static IMaestroApi GetAnonymous(string baseUri)
        {
            return new NoCredentialsMaestroApi(new Uri(baseUri));
        }
    }
}
