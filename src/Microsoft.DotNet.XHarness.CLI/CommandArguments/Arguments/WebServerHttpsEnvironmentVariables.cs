// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

internal class WebServerHttpsEnvironmentVariables : Argument<IEnumerable<string>>
{
    public WebServerHttpsEnvironmentVariables()
        : base(
              "set-web-server-https-env=",
              "Comma separated list of environment variable names, which should be set to HTTPS host and port, for the unit test, which use xharness as test web server",
              Array.Empty<string>())
    {
    }

    public override void Action(string argumentValue) => Value = argumentValue.Split(',');
}
