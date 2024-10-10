// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public class SimDeviceType
{
    public string Name { get; }
    public string Identifier { get; }
    public string ProductFamilyId { get; }
    public long MinRuntimeVersion { get; }
    public long MaxRuntimeVersion { get; }
    public bool Supports64Bits { get; }

    public SimDeviceType(string name, string identifier, string productFamilyId, long minRuntimeVersion, long maxRuntimeVersion, bool supports64Bits)
    {
        Name = name;
        Identifier = identifier;
        ProductFamilyId = productFamilyId;
        MinRuntimeVersion = minRuntimeVersion;
        MaxRuntimeVersion = maxRuntimeVersion;
        Supports64Bits = supports64Bits;
    }
}
