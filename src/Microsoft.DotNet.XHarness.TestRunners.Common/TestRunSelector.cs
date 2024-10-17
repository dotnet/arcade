// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

internal class TestRunSelector
{
    public string Assembly { get; set; }
    public string Value { get; set; }
    public TestRunSelectorType Type { get; set; }
    public bool Include { get; set; }
}
