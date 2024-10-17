// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Apple;

/// <summary>
/// Specifies the channel that is used to communicate with the device.
/// </summary>
public enum CommunicationChannel
{
    /// <summary>
    /// Connect to the device using the LAN or WAN.
    /// </summary>
    Network,
    /// <summary>
    /// Connect to the device using a tcp-tunnel
    /// </summary>
    UsbTunnel,
}
