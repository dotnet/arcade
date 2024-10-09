// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Contains information about a single test failure. Information is used at the end of the run to print
/// summary of failures to logcat as well as put them in the results Bundle. <see cref="TestName"/> is used
/// only to generate unique index when storing information in the Bundle, so <see cref="Message"/> must contain
/// the test name.
/// </summary>
public class TestFailureInfo
{
    /// <summary>
    /// Gets or sets the name of the test. Must not be null or empty (all whitespace isn't allowed either)
    /// </summary>
    /// <value>The name of the test.</value>
    public string TestName { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string Message { get; set; }

    /// <summary>
    /// Gets a value indicating whether this <see cref="T:Xamarin.Android.UnitTests.TestFailureInfo"/> has info
    /// about failure.
    /// </summary>
    /// <value><c>true</c> info exists; otherwise, <c>false</c>.</value>
    public bool HasInfo => !string.IsNullOrEmpty(TestName?.Trim()) && !string.IsNullOrEmpty(Message);
}
