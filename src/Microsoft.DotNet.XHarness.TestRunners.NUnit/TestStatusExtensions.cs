// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common;
using NUnit.Framework.Interfaces;

namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

internal static class TestStatusExtensions
{
    public static string ToXmlResultValue(this TestStatus status, XmlResultJargon jargon) => jargon switch
    {
        XmlResultJargon.NUnitV2 => status switch
        {
            TestStatus.Failed => "Failure",
            TestStatus.Inconclusive => "Inconclusive",
            TestStatus.Passed => "Success",
            TestStatus.Skipped => "Ignored",
            _ => "Failure"
        },
        XmlResultJargon.NUnitV3 => status switch
        {
            TestStatus.Failed => "Failed",
            TestStatus.Inconclusive => "Inconclusive",
            TestStatus.Passed => "Passed",
            TestStatus.Skipped => "Skipped",
            _ => "Failed",
        },
        XmlResultJargon.xUnit => status switch
        {
            TestStatus.Failed => "Fail",
            TestStatus.Inconclusive => "Skip",
            TestStatus.Passed => "Pass",
            TestStatus.Skipped => "Skip",
            _ => "Fail",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
