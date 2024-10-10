// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests;

public class ExitCodeDetectorTests : IDisposable
{
    private readonly string? _tempFilename = null;

    [Fact]
    public void ExitCodeIsDetectedTest()
    {
        var appBundleInformation = new AppBundleInformation("HelloiOS", "net.dot.HelloiOS", "some/path", "some/path", false, null);

        var detector = new iOSExitCodeDetector();

        var log = new[]
        {
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 200",
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3[67121]): Service exited with abnormal code: 1",
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds.",
        };

        var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

        Assert.Equal(200, exitCode);
    }

    [Fact]
    public void ExitCodeIsDetectedOnMacCatalystTest()
    {
        var appBundleInformation = new AppBundleInformation("System.Buffers.Tests", "net.dot.System.Buffers.Tests", "some/path", "some/path", false, null);

        var detector = new MacCatalystExitCodeDetector();

        var log = new[]
        {
            "Feb 18 06:40:16 Admins-Mac-Mini System.Buffers.Tests[59229]: CDN - client insert callback function client = 0 type = 17 function = 0x7fff3b262246 local_olny = false",
            "Feb 18 06:40:16 Admins-Mac-Mini System.Buffers.Tests[59229]: CDN - client setup_remote_port",
            "Feb 18 06:40:16 Admins-Mac-Mini System.Buffers.Tests[59229]: CDN - Bootstrap Port: 1799",
            "Feb 18 06:40:16 Admins-Mac-Mini System.Buffers.Tests[59229]: CDN - Remote Port: 56835 (com.apple.CoreDisplay.Notification)",
            "Feb 18 06:40:16 Admins-Mac-Mini System.Buffers.Tests[59229]: CDN - client setup_local_port",
            "Feb 18 06:40:16 Admins-Mac-Mini System.Buffers.Tests[59229]: CDN - Local Port: 78339",
            "Feb 18 06:40:16 Admins-Mac-Mini com.apple.xpc.launchd[1] (net.dot.System.Buffers.Tests.15140[59229]): Service exited with abnormal code: 74",
            "Feb 18 06:40:49 Admins-Mac-Mini com.apple.xpc.launchd[1] (com.apple.mdworker.shared.09000000-0600-0000-0000-000000000000[59231]): Service exited due to SIGKILL | sent by mds[88]",
            "Feb 18 06:40:58 Admins-Mac-Mini com.apple.xpc.launchd[1] (com.apple.mdworker.shared.02000000-0100-0000-0000-000000000000[59232]): Service exited due to SIGKILL | sent by mds[88]",
            "Feb 18 06:41:01 Admins-Mac-Mini com.apple.xpc.launchd[1] (com.apple.mdworker.shared.0D000000-0000-0000-0000-000000000000[59237]): Service exited due to SIGKILL | sent by mds[88]",
            "Feb 18 06:41:23 Admins-Mac-Mini System.Buffers.Tests[59248]: CDN - client insert callback function client = 0 type = 17 function = 0x7fff3b262246 local_olny = false",
            "Feb 18 06:41:23 Admins-Mac-Mini System.Buffers.Tests[59248]: CDN - client setup_remote_port",
            "Feb 18 06:41:23 Admins-Mac-Mini System.Buffers.Tests[59248]: CDN - Bootstrap Port: 1799",
            "Feb 18 06:41:23 Admins-Mac-Mini System.Buffers.Tests[59248]: CDN - Remote Port: 75271 (com.apple.CoreDisplay.Notification)",
            "Feb 18 06:41:23 Admins-Mac-Mini System.Buffers.Tests[59248]: CDN - client setup_local_port",
            "Feb 18 06:41:23 Admins-Mac-Mini System.Buffers.Tests[59248]: CDN - Local Port: 52995",
        };

        var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

        Assert.Equal(74, exitCode);
    }

    [Fact]
    public void NegativeExitCodeIsDetectedTest()
    {
        var appBundleInformation = new AppBundleInformation("HelloiOS", "net.dot.HelloiOS", "some/path", "some/path", false, null);

        var detector = new iOSExitCodeDetector();

        var log = new[]
        {
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: -2",
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3[67121]): Service exited with abnormal code: 1",
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds.",
        };

        var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

        Assert.Equal(-2, exitCode);
    }

    [Fact]
    public void iOSDeviceCodeIsDetectedTest()
    {
        var appBundleInformation = new AppBundleInformation("iOS.Simulator.PInvoke.Test", "net.dot.iOS.Simulator.PInvoke.Test", "some/path", "some/path", false, null);

        var detector = new iOSExitCodeDetector();

        var log = new[]
        {
            "[07:02:15.9749990] Xamarin.Hosting: Mounting developer image on 'DNCENGOSX-003'",
            "[07:02:15.9752160] Xamarin.Hosting: Mounted developer image on 'DNCENGOSX-003'",
            "[07:02:16.5177370] Xamarin.Hosting: Launched net.dot.Some.Other.App with PID: 13942",
            "[07:02:16.5181560] Launched application 'net.dot.Some.Other.App' on 'DNCENGOSX-003' with pid 13942",
            "[07:02:16.6150270] 2022-03-30 07:02:16.601 Some.Other.App[13942:136284382] Done!",
            "[07:02:21.6632630] Xamarin.Hosting: Process '13942' exited with exit code 143 or crashing signal .",
            "[07:02:21.6637600] Application 'net.dot.Some.Other.App' terminated (with exit code '143' and/or crashing signal ').",

            // We care about this run
            "[07:02:15.9749990] Xamarin.Hosting: Mounting developer image on 'DNCENGOSX-003'",
            "[07:02:15.9752160] Xamarin.Hosting: Mounted developer image on 'DNCENGOSX-003'",
            "[07:02:16.5177370] Xamarin.Hosting: Launched net.dot.iOS.Simulator.PInvoke.Test with PID: 83937",
            "[07:02:16.5181560] Launched application 'net.dot.iOS.Simulator.PInvoke.Test' on 'DNCENGOSX-003' with pid 83937",
            "[07:02:16.6150270] 2022-03-30 07:02:16.601 iOS.Simulator.PInvoke.Test[83937:136284382] Done!",
            "[07:02:21.6632630] Xamarin.Hosting: Process '83937' exited with exit code 42 or crashing signal .",
            "[07:02:21.6637600] Application 'net.dot.iOS.Simulator.PInvoke.Test' terminated (with exit code '42' and/or crashing signal ').",
        };

        var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

        Assert.Equal(42, exitCode);
    }

    [Fact]
    public void ExitCodeIsNotDetectedTest()
    {
        var appBundleInformation = new AppBundleInformation("HelloiOS", "net.dot.HelloiOS", "some/path", "some/path", false, null);

        var detector = new iOSExitCodeDetector();

        var log = new[]
        {
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined.",
            "Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Some other error message",
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3[67121]): Service exited with abnormal code: 1",
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds.",
        };

        var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

        Assert.Null(exitCode);
    }

    [Fact]
    public void ExitCodeFromPreviousRunIsIgnored()
    {
        var previousLog =
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined." + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined." + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined." + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Some other error message" + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 55" + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds." + Environment.NewLine;

        var currentRunLog =
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined." + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined." + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined." + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Some other error message" + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 72" + Environment.NewLine +
            "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds." + Environment.NewLine;

        var tempFilename = Path.GetTempFileName();
        File.WriteAllText(tempFilename, previousLog);

        var capturedFilename = Path.GetTempFileName();

        using var captureLog = new CaptureLog(capturedFilename, tempFilename, false);
        captureLog.StartCapture();
        File.AppendAllText(tempFilename, currentRunLog);
        captureLog.StopCapture();

        var appBundleInformation = new AppBundleInformation("net.dot.HelloiOS", "net.dot.HelloiOS", "some/path", "some/path", false, null);
        var exitCode = new iOSExitCodeDetector().DetectExitCode(appBundleInformation, captureLog);

        Assert.Equal(72, exitCode);
    }

    public void Dispose()
    {
        if (_tempFilename != null)
        {
            File.Delete(_tempFilename);
        }
        GC.SuppressFinalize(this);
    }

    private static IFileBackedLog GetLogMock(string[] loglines)
    {
        byte[] byteArray = Encoding.ASCII.GetBytes(string.Join(Environment.NewLine, loglines));
        var stream = new MemoryStream(byteArray);
        var reader = new StreamReader(stream);

        var mock = new Mock<IFileBackedLog>();

        mock.Setup(x => x.GetReader()).Returns(reader);

        return mock.Object;
    }
}
