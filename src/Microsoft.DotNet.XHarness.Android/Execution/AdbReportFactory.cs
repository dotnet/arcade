// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution;

internal class AdbReportFactory
{
    // This method return proper ReportManager based on API number of current device
    // It allows to apply different logic for bugreport generation on API 21-23 and above
    internal static IReportManager CreateReportManager(ILogger log, int api)
    {
        if (api > 23) return new NewReportManager(log);
        else return new Api23AndOlderReportManager(log);
    }
}
