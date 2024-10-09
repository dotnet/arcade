// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests;

public class DeviceFinderTests
{
    private readonly IDeviceFinder _deviceFinder;
    private readonly Mock<IHardwareDeviceLoader> _deviceLoader;
    private readonly Mock<ISimulatorLoader> _simulatorLoader;

    private readonly List<IHardwareDevice> _hardwareDevices = new();

    public DeviceFinderTests()
    {
        _deviceLoader = new Mock<IHardwareDeviceLoader>();
        _deviceLoader
            .Setup(x => x.LoadDevices(It.IsAny<ILog>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _deviceLoader
            .SetupGet(x => x.ConnectedDevices)
            .Returns(_hardwareDevices);
        _deviceLoader
            .SetupGet(x => x.ConnectedTV)
            .Returns(_hardwareDevices.Where(d => d.DevicePlatform == DevicePlatform.tvOS));
        _deviceLoader
            .SetupGet(x => x.Connected64BitIOS)
            .Returns(_hardwareDevices.Where(d => d.DevicePlatform == DevicePlatform.iOS && d.Architecture == Architecture.ARM64));

        _simulatorLoader = new Mock<ISimulatorLoader>();
        _deviceFinder = new DeviceFinder(_deviceLoader.Object, _simulatorLoader.Object);
    }

    [Fact]
    public async Task CorrectTypeOfDeviceIsFoundTest()
    {
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "8A450AA31EA94191AD6B02455F377CC1"));
        _hardwareDevices.Add(CreateDevice(DeviceClass.AppleTV, "30C0630E03EB40A19F9A40D61E66796B"));

        var device = await _deviceFinder.FindDevice(new TestTargetOs(TestTarget.Device_tvOS, null), null, new MemoryLog(), false);
        Assert.Equal("30C0630E03EB40A19F9A40D61E66796B", device.Device.UDID);

        _hardwareDevices.Clear();
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "8A450AA31EA94191AD6B02455F377CC1"));
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "30C0630E03EB40A19F9A40D61E66796B"));

        await Assert.ThrowsAsync<NoDeviceFoundException>(async () => await _deviceFinder.FindDevice(new TestTargetOs(TestTarget.Device_tvOS, null), null, new MemoryLog(), false));
    }

    [Fact]
    public async Task DeviceIsFoundByNameTest()
    {
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "8A450AA31EA94191AD6B02455F377CC1"));
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "30C0630E03EB40A19F9A40D61E66796B"));

        var device = await _deviceFinder.FindDevice(new TestTargetOs(TestTarget.Device_iOS, null), "30C0630E03EB40A19F9A40D61E66796B", new MemoryLog(), false);
        Assert.Equal("30C0630E03EB40A19F9A40D61E66796B", device.Device.UDID);
        await Assert.ThrowsAsync<NoDeviceFoundException>(async () => await _deviceFinder.FindDevice(new TestTargetOs(TestTarget.Device_iOS, null), "unknown", new MemoryLog(), false));
    }

    [Fact]
    public async Task OnlyPairedDevicesAreFoundTest()
    {
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "8A450AA31EA94191AD6B02455F377CC1", false));
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "30C0630E03EB40A19F9A40D61E66796B"));

        var device = await _deviceFinder.FindDevice(new TestTargetOs(TestTarget.Device_iOS, null), null, new MemoryLog(), false);
        Assert.Equal("30C0630E03EB40A19F9A40D61E66796B", device.Device.UDID);

        _hardwareDevices.Clear();
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "8A450AA31EA94191AD6B02455F377CC1", false));
        _hardwareDevices.Add(CreateDevice(DeviceClass.iPhone, "30C0630E03EB40A19F9A40D61E66796B", false));

        await Assert.ThrowsAsync<NoDeviceFoundException>(async () => await _deviceFinder.FindDevice(new TestTargetOs(TestTarget.Device_iOS, null), null, new MemoryLog(), false));
    }

    private static IHardwareDevice CreateDevice(DeviceClass deviceClass, string udid, bool isPaired = true) =>
        new Device(
            buildVersion: "17A577",
            deviceClass: deviceClass,
            deviceIdentifier: udid,
            interfaceType: "USB",
            isUsableForDebugging: true,
            name: "Test iPhone",
            productType: "iPhone12,1",
            productVersion: "12.1",
            isPaired: isPaired);
}
