// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

internal class WebServerUseCrossOriginPolicyArguments : SwitchArgument
{
    public WebServerUseCrossOriginPolicyArguments()
        : base("web-server-use-cop", "Sets Cross-Origin-Opener-Policy=same-origin and Cross-Origin-Embedder-Policy=require-corp", false)
    {
    }
}
