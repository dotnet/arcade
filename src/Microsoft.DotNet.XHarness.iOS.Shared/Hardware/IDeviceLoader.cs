// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public interface IDeviceLoader
{
    Task LoadDevices(
        ILog log,
        bool includeLocked = false,
        bool forceRefresh = false,
        bool listExtraData = false,
        bool includeWirelessDevices = true,
        CancellationToken cancellationToken = default);
}
