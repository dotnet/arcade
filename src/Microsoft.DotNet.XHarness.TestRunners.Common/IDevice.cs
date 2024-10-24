// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Interface to be implemented by those classes that provide the required
/// information of the device that is being used so that we can add the
/// device information in the test logs.
/// </summary>
public interface IDevice
{
    string BundleIdentifier { get; }
    string UniqueIdentifier { get; }
    string Name { get; }
    string Model { get; }
    string SystemName { get; }
    string SystemVersion { get; }
    string Locale { get; }
}
