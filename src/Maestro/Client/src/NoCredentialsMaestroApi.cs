// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;

namespace Microsoft.DotNet.Maestro.Client
{
    internal class NoCredentialsMaestroApi : MaestroApi
    {
        public NoCredentialsMaestroApi(params DelegatingHandler[] handlers) : base(handlers)
        {
        }

        public NoCredentialsMaestroApi(Uri baseUri, params DelegatingHandler[] handlers) : base(baseUri, handlers)
        {
        }
    }
}
