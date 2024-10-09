using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests;

public class ErrorKnowledgeBaseTests : IDisposable
{
    private readonly ErrorKnowledgeBase _errorKnowledgeBase;
    private readonly string _logPath = Path.GetTempFileName();

    public ErrorKnowledgeBaseTests()
    {
        _errorKnowledgeBase = new ErrorKnowledgeBase();
    }

    public void Dispose()
    {
        if (File.Exists(_logPath))
        {
            File.Delete(_logPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WrongArchPresentTest()
    {
        var expectedFailureMessage =
            "IncorrectArchitecture: Failed to find matching device arch for the application";
        using (var log = new LogFile("test", _logPath))
        {
            // write some data in it
            log.WriteLine("InstallingEmbeddedProfile: 65%");
            log.WriteLine("PercentComplete: 30");
            log.WriteLine("Status: InstallingEmbeddedProfile");
            log.WriteLine("VerifyingApplication: 70%");
            log.WriteLine("PercentComplete: 40");
            log.WriteLine("Status: VerifyingApplication");
            log.WriteLine(
                "IncorrectArchitecture: Failed to find matching arch for 64-bit Mach-O input file /private/var/installd/Library/Caches/com.apple.mobile.installd.staging/temp.Ic8Ank/extracted/monotouchtest.app/monotouchtest");
            log.Flush();

            Assert.True(_errorKnowledgeBase.IsKnownInstallIssue(log, out var failure));
            Assert.Equal(expectedFailureMessage, failure?.HumanMessage);
        }
    }

    [Fact]
    public void WrongArchNotPresentTest()
    {
        using (var log = new LogFile("test", _logPath))
        {
            // write some data in it
            log.WriteLine("InstallingEmbeddedProfile: 65%");
            log.WriteLine("PercentComplete: 30");
            log.WriteLine("Status: InstallingEmbeddedProfile");
            log.WriteLine("VerifyingApplication: 70%");
            log.WriteLine("PercentComplete: 40");
            log.WriteLine("Status: VerifyingApplication");
            log.Flush();

            Assert.False(_errorKnowledgeBase.IsKnownInstallIssue(log, out var failure));
            Assert.Null(failure);
        }
    }

    [Fact]
    public void UsbIssuesPresentTest()
    {
        var expectedFailureMessage =
            "Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device";
        using (var log = new LogFile("test", _logPath))
        {
            // initial lines are not interesting
            log.WriteLine("InstallingEmbeddedProfile: 65%");
            log.WriteLine("PercentComplete: 30");
            log.WriteLine("Status: InstallingEmbeddedProfile");
            log.WriteLine("VerifyingApplication: 70%");
            log.WriteLine("PercentComplete: 40");
            log.WriteLine("Xamarin.Hosting.MobileDeviceException: Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device (error: 0xe8000065 kAMDMuxConnectError)");
            log.Flush();

            Assert.True(_errorKnowledgeBase.IsKnownTestIssue(log, out var failure));
            Assert.Equal(expectedFailureMessage, failure?.HumanMessage);
        }
    }

    [Fact]
    public void UsbIssuesMissingTest()
    {
        using (var log = new LogFile("test", _logPath))
        {
            // initial lines are not interesting
            log.WriteLine("InstallingEmbeddedProfile: 65%");
            log.WriteLine("PercentComplete: 30");
            log.WriteLine("Status: InstallingEmbeddedProfile");
            log.WriteLine("VerifyingApplication: 70%");
            log.WriteLine("PercentComplete: 40");
            log.Flush();

            Assert.False(_errorKnowledgeBase.IsKnownTestIssue(log, out var failure));
            Assert.Null(failure);
        }
    }

    [Fact]
    public void DeviceLockedTest()
    {
        var expectedFailureMessage = "Cannot launch the application because the device is locked. Please unlock the device and try again";
        using (var log = new LogFile("test", _logPath))
        {
            log.WriteLine("05:55:56.7712200 05:55:56.7712030 Xamarin.Hosting: Mounting developer image on 'iPremek'");
            log.WriteLine("05:55:56.7716040 05:55:56.7715960 Xamarin.Hosting: Mounted developer image on 'iPremek'");
            log.WriteLine("05:55:56.8494160 05:55:56.8494020 error MT1031: Could not launch the app 'net.dot.HelloiOS' on the device 'iPremek' because the device is locked. Please unlock the device and try again.");
            log.WriteLine("05:55:56.8537390 05:55:56.8537300   at Xamarin.Launcher.DevController+<>c__DisplayClass14_0.<LaunchBundleOnDevice>b__0 () [0x0059d] in /Users/rolf/work/maccore/xcode12/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/controller-device.cs:372");
            log.WriteLine("05:55:56.8537720 05:55:56.8537700   at Xamarin.Launcher.DevController.LaunchDeviceBundleAsync (System.String app_path, Xamarin.Hosting.DeviceLaunchConfig config) [0x00111] in /Users/rolf/work/maccore/xcode12/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/controller-device.cs:176");
            log.WriteLine("05:55:56.8537800 05:55:56.8537790   at Xamarin.Utils.NSRunLoopExtensions.RunUntilTaskCompletion[T] (Foundation.NSRunLoop this, System.Threading.Tasks.Task`1[TResult] task) [0x00082] in /Users/rolf/work/maccore/xcode12/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Utils/Extensions.cs:35");
            log.WriteLine("05:55:56.8537870 05:55:56.8537860   at Xamarin.Launcher.Driver.Main2 (System.String[] args) [0x00b43] in /Users/rolf/work/maccore/xcode12/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/Main.cs:458");
            log.WriteLine("05:55:56.8538290 05:55:56.8538250   at Xamarin.Launcher.Driver.Main (System.String[] args) [0x0006d] in /Users/rolf/work/maccore/xcode12/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/Main.cs:150");
            log.WriteLine("05:55:56.8618920 05:55:56.8618780 Process mlaunch exited with 1");
            log.WriteLine("05:56:01.8765370 05:56:01.8765240 Killing process tree of 2797...");
            log.WriteLine("05:56:01.8938830 05:56:01.8938670 Pids to kill: 2797");
            log.Flush();

            Assert.True(_errorKnowledgeBase.IsKnownTestIssue(log, out var failure));
            Assert.Equal(expectedFailureMessage, failure?.HumanMessage);
        }
    }

    [Fact]
    public void DeviceUpdateNotFinishedTest()
    {
        var expectedFailureMessage = "Cannot launch the application because the device's update hasn't been finished. The setup assistant is still running. Please finish the device OS update on the device";
        using (var log = new LogFile("test", _logPath))
        {
            log.WriteLine("[08:44:10.0123870] Xamarin.Hosting: Mounted developer image on 'DNCENGTVOS-090'");
            log.WriteLine("[08:44:10.7259750] warning MT1043: Failed to launch the application using the instruments service. Will try launching the app using gdb service.");
            log.WriteLine("[08:44:10.7260060]         ");
            log.WriteLine("[08:44:10.7260580] --- inner exception");
            log.WriteLine("[08:44:10.7265060] error HE0003: Failed to launch the application 'net.dot.System.Buffers.Tests' on 'DNCENGTVOS-090: 1 (Request to launch net.dot.System.Buffers.Tests failed.)");
            log.WriteLine("[08:44:10.7265220]         ");
            log.WriteLine("[08:44:10.7265280] ---");
            log.WriteLine("[08:44:10.7278170] Launching 'net.dot.System.Buffers.Tests' on the device 'DNCENGTVOS-090'");
            log.WriteLine("[08:44:11.3152170] Launching /private/var/containers/Bundle/Application/27FA8535-C645-413A-ABE9-2ABD0BC6086B/System.Buffers.Tests.app");
            log.WriteLine("[08:44:11.3156130] Xamarin.Hosting: Sending command: $A208,0,2f707269766174652f7661722f636f6e7461696e6572732f42756e646c652f4170706c69636174696f6e2f32374641383533352d433634352d343133412d414245392d3241424430424336303836422f53797374656d2e427566666572732e54657374732e617070#43");
            log.WriteLine("[08:44:11.3174180] Xamarin.Hosting: Received command: OK");
            log.WriteLine("[08:44:11.3174360] Xamarin.Hosting: Sending command: $qLaunchSuccess#a5");
            log.WriteLine("[08:44:11.3864920] Xamarin.Hosting: Received command: EThe operation couldn’t be completed. [PBD] Denying open-application request for reason: Disabled (Cannot launch app 'net.dot.System.Buffers.Tests' while Setup Assistant is running)");
            log.WriteLine("[08:44:11.3886900] error MT1007: Failed to launch the application 'net.dot.System.Buffers.Tests' on the device 'DNCENGTVOS-090': Failed to launch the application 'net.dot.System.Buffers.Tests' on the device 'DNCENGTVOS-090': Application failed to launch: EThe operation couldn’t be completed. [PBD] Denying open-application request for reason: Disabled (Cannot launch app 'net.dot.System.Buffers.Tests' while Setup Assistant is running)");
            log.WriteLine("[08:44:11.3887100]         ");
            log.WriteLine("[08:44:11.3887170]         . You can still launch the application manually by tapping on it.");
            log.WriteLine("[08:44:11.3887220]         ");
            log.WriteLine("[08:44:11.3887260] --- inner exception");
            log.WriteLine("[08:44:11.3887310] error MT1020: Failed to launch the application 'net.dot.System.Buffers.Tests' on the device 'DNCENGTVOS-090': Application failed to launch: EThe operation couldn’t be completed. [PBD] Denying open-application request for reason: Disabled (Cannot launch app 'net.dot.System.Buffers.Tests' while Setup Assistant is running)");
            log.WriteLine("[08:44:11.3887360]         ");
            log.WriteLine("[08:44:11.3887400]         ");
            log.WriteLine("[08:44:11.3887450] ---");
            log.WriteLine("[08:44:11.3887490] --- inner exception");
            log.WriteLine("[08:44:11.3887570] error HE1107: Application failed to launch: EThe operation couldn’t be completed. [PBD] Denying open-application request for reason: Disabled (Cannot launch app 'net.dot.System.Buffers.Tests' while Setup Assistant is running)");
            log.WriteLine("[08:44:11.3887790]         ");
            log.WriteLine("[08:44:11.3887840] ---");
            log.WriteLine("[08:44:11.3917790]   at Xamarin.Launcher.DevController.LaunchDeviceBundleIdAsync (System.String bundle_id, Xamarin.Hosting.DeviceLaunchConfig config) [0x001cc] in /Users/builder/azdo/_work/1/s/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/controller-device.cs:208 ");
            log.WriteLine("[08:44:11.3917960]   at Xamarin.Utils.NSRunLoopExtensions.RunUntilTaskCompletion[T] (Foundation.NSRunLoop this, System.Threading.Tasks.Task`1[TResult] task) [0x00082] in /Users/builder/azdo/_work/1/s/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Utils/Extensions.cs:35 ");
            log.WriteLine("[08:44:11.3918040]   at Xamarin.Launcher.Driver.Main2 (System.String[] args) [0x00b43] in /Users/builder/azdo/_work/1/s/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/Main.cs:459 ");
            log.WriteLine("[08:44:11.3918090]   at Xamarin.Launcher.Driver.Main (System.String[] args) [0x0006d] in /Users/builder/azdo/_work/1/s/maccore/tools/mlaunch/Xamarin.Hosting/Xamarin.Launcher/Main.cs:151 ");
            log.Flush();

            Assert.True(_errorKnowledgeBase.IsKnownTestIssue(log, out var failure));
            Assert.Equal(expectedFailureMessage, failure?.HumanMessage);
        }
    }
}
