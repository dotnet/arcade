// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public interface IServiceConfigSection
    {
        string this[string name] { get; }
    }
}
