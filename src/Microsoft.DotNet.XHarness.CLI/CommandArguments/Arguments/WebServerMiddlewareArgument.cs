// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

internal class WebServerMiddlewareArgument : TypeFromAssemblyArgument<Type>
{
    public WebServerMiddlewareArgument()
        : base(
              "web-server-middleware=",
              "<path>,<typeName> to assembly and type which contains Kestrel middleware for local test server. Could be used multiple times to load multiple middlewares",
              repeatable: true)
    {
    }
}
