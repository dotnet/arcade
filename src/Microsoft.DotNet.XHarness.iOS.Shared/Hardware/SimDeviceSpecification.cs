// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public class SimDeviceSpecification
{
    public SimulatorDevice Main { get; }
    public SimulatorDevice Companion { get; } // the phone for watch devices

    public SimDeviceSpecification(SimulatorDevice main, SimulatorDevice companion)
    {
        Main = main;
        Companion = companion;
    }
}
