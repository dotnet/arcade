// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware;

public class DeviceTest
{
    public class DevicesDataTestSource
    {
        public static IEnumerable<object[]> DebugSpeedDevices
        {
            get
            {
                var data = new[] {
                        (interfaceType: "usb", result: 0),
                        (interfaceType: "USB", result: 0),
                        (interfaceType: "wifi", result:  2),
                        (interfaceType: "WIFI", result: 2),
                        (interfaceType: (string) null, result:  1),
                        (interfaceType: "HOLA", result: 3),
                    };

                foreach (var (interfaceType, result) in data)
                {
                    yield return new object[]
                    {
                            new Device(
                                buildVersion: "17A577",
                                deviceClass: DeviceClass.iPhone,
                                deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
                                interfaceType: interfaceType,
                                isUsableForDebugging: true,
                                name: "Test iPhone",
                                productType: "iPhone12,1",
                                productVersion: "13.0"),
                            result
                    };
                }
            }
        }

        public static IEnumerable<object[]> DevicePlatformDevices
        {
            get
            {
                var data = new[] {
                        (deviceClass: DeviceClass.iPhone, result: DevicePlatform.iOS),
                        (deviceClass: DeviceClass.iPod, result: DevicePlatform.iOS),
                        (deviceClass: DeviceClass.iPad, result: DevicePlatform.iOS),
                        (deviceClass: DeviceClass.AppleTV, result: DevicePlatform.tvOS),
                        (deviceClass: DeviceClass.Watch, result: DevicePlatform.watchOS),
                        (deviceClass: DeviceClass.xrOS, result: DevicePlatform.xrOS),
                        (deviceClass: DeviceClass.Unknown, result: DevicePlatform.Unknown),
                    };

                foreach (var (deviceClass, result) in data)
                {
                    yield return new object[]
                    {
                            new Device(
                                buildVersion: "17A577",
                                deviceClass: deviceClass,
                                deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
                                interfaceType: "USB",
                                isUsableForDebugging: true,
                                name: "Test iPhone",
                                productType: "iPhone12,1",
                                productVersion: "13.0"),
                            result
                    };
                }
            }
        }

        public static IEnumerable<object[]> Supports64bDevices
        {
            get
            {
                var data = new Dictionary<string, (string version, bool result)[]>()
                {
                    ["iPhone"] = new[] {
                            (version: "1,1", result: false),
                            (version: "1,2", result: false),
                            (version: "2,1", result: false),
                            (version: "3,1", result: false),
                            (version: "3,2", result: false),
                            (version: "3,3", result: false),
                            (version: "4,1", result: false),
                            (version: "5,1", result: false),
                            (version: "5,2", result: false),
                            (version: "5,3", result: false),
                            (version: "6,1", result: true),
                            (version: "6,2", result: true),
                            (version: "7,1", result: true),
                            (version: "7,2", result: true),
                            (version: "8,4", result: true),
                            (version: "9,1", result: true),
                            (version: "9,2", result: true),
                            (version: "10,1", result: true),
                            (version: "11,1", result: true),
                            (version: "12,1", result: true),
                        },
                    ["iPad"] = new[] {
                            (version: "1,1", result: false),
                            (version: "1,2", result: false),
                            (version: "2,1", result: false),
                            (version: "3,1", result: false),
                            (version: "3,2", result: false),
                            (version: "3,3", result: false),
                            (version: "4,1", result: true),
                            (version: "4,2", result: true),
                            (version: "5,1", result: true),
                            (version: "6,1", result: true),
                            (version: "6,3", result: true),
                            (version: "7,1", result: true),
                        },
                    ["iPod"] = new[] {
                            (version: "1,1", result: false),
                            (version: "1,2", result: false),
                            (version: "2,1", result: false),
                            (version: "3,3", result: false),
                            (version: "4,1", result: false),
                            (version: "5,1", result: false),
                            (version: "5,2", result: false),
                            (version: "7,1", result: true),
                            (version: "7,2", result: true),
                        },
                    ["AppleTV"] = new[] {
                            (version: "1,1", result: true),
                            (version: "2,1", result: true),
                            (version: "3,1", result: true),
                        },
                    ["Watch"] = new[] {
                            (version: "1,1", result: false),
                            (version: "1,2", result: false),
                            (version: "2,1", result: false),
                            (version: "3,1", result: false),
                            (version: "3,2", result: false),
                            (version: "3,3", result: false),
                            (version: "4,1", result: false),
                            (version: "4,2", result: false),

                        }
                };

                foreach (var product in data.Keys)
                {
                    foreach (var (version, result) in data[product])
                    {
                        yield return new object[]
                        {
                                new Device(
                                    buildVersion: "17A577",
                                    deviceClass: DeviceClass.iPhone,
                                    deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
                                    interfaceType: "USB",
                                    isUsableForDebugging: true,
                                    name: "Test iPhone",
                                    productType: $"{product}{version}",
                                    productVersion: "13.0"),
                                result
                        };
                    }
                }
            }
        }

        public static IEnumerable<object[]> Supports32bDevices
        {
            get
            {
                var iOSCommon = new[] {
                            (version: new Version (1,1), result: true),
                            (version: new Version (2,1), result: true),
                            (version: new Version (3,1), result: true),
                            (version: new Version (4,1), result: true),
                            (version: new Version (5,1), result: true),
                            (version: new Version (6,1), result: true),
                            (version: new Version (7,1), result: true),
                            (version: new Version (8,1), result: true),
                            (version: new Version (11,1), result: false),
                            (version: new Version (11,2), result: false),
                            (version: new Version (12,1), result: false),
                        };
                var data = new Dictionary<DeviceClass, (Version version, bool result)[]>
                {
                    [DeviceClass.iPhone] = iOSCommon,
                    [DeviceClass.iPad] = iOSCommon,
                    [DeviceClass.iPod] = iOSCommon,
                    [DeviceClass.AppleTV] = new[] {
                            (version: new Version (1,1), result: false),
                            (version: new Version (2,1), result: false),
                            (version: new Version (3,1), result: false),
                            (version: new Version (4,1), result: false),
                        },
                    [DeviceClass.Watch] = new[] {
                            (version: new Version (1,1), result: true),
                            (version: new Version (2,1), result: true),
                            (version: new Version (3,1), result: true),
                            (version: new Version (4,1), result: true),
                        }
                };

                foreach (var deviceClass in data.Keys)
                {
                    foreach (var (version, result) in data[deviceClass])
                    {
                        yield return new object[]
                        {
                                new Device(
                                    buildVersion: "17A577",
                                    deviceClass: deviceClass,
                                    deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
                                    interfaceType: "USB",
                                    isUsableForDebugging: true,
                                    name: "Test iPhone",
                                    productType: "iPhone12,1",
                                    productVersion: version.ToString()),
                                result
                        };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(DebugSpeedDevices), MemberType = typeof(DevicesDataTestSource))]
        public void DebugSpeedTest(IHardwareDevice device, int expected) => Assert.Equal(expected, device.DebugSpeed);

        [Theory]
        [MemberData(nameof(DevicePlatformDevices), MemberType = typeof(DevicesDataTestSource))]
        public void DevicePlatformTest(IHardwareDevice device, DevicePlatform expected) => Assert.Equal(expected, device.DevicePlatform);

        [Theory]
        [MemberData(nameof(Supports64bDevices), MemberType = typeof(DevicesDataTestSource))]
        public void Supports64bTest(IHardwareDevice device, bool expected) => Assert.Equal(expected, device.Supports64Bit);

        [Theory]
        [MemberData(nameof(Supports32bDevices), MemberType = typeof(DevicesDataTestSource))]
        public void Supports32BTest(IHardwareDevice device, bool expected) => Assert.Equal(expected, device.Supports32Bit);
    }
}
