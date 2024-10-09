// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public class SimRuntime
{
    public string Name { get; }
    public string Identifier { get; }
    public long Version { get; }

    public SimRuntime(string name, string identifier, long version)
    {
        Name = name;
        Identifier = identifier;
        Version = version;
    }
}
