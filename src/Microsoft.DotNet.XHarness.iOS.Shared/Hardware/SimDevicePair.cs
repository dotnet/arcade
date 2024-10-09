// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public class SimDevicePair
{
    public string UDID { get; }
    public string Companion { get; }
    public string Gizmo { get; }

    public SimDevicePair(string UDID, string companion, string gizmo)
    {
        this.UDID = UDID;
        Companion = companion;
        Gizmo = gizmo;
    }
}
