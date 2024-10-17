// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public static class WrenchLog
{

    public static void WriteLine(string message, params object[] args) => WriteLine(string.Format(message, args));

    public static void WriteLine(string message)
    {
        // disabled, might be deleted later.
    }
}
