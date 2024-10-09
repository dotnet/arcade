// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class AppBundleInformationParserTests : IDisposable
{
    private const string AppName = "com.xamarin.bcltests.SystemXunit";
    private const string Executable = "SystemXunit.bcltests.xamarin.com";
    private static readonly string s_outputPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(AppBundleInformationParser)).Location);
    private static readonly string s_sampleProjectPath = Path.Combine(s_outputPath, "Samples", "TestProject");
    private static readonly string s_appPath = Path.Combine(s_sampleProjectPath, "bin", AppName + ".app");
    private static readonly string s_appPath2 = Path.Combine(s_sampleProjectPath, "bin2", AppName + ".app");
    private static readonly string s_projectFilePath = Path.Combine(s_sampleProjectPath, "SystemXunit.csproj");

    public AppBundleInformationParserTests()
    {
        Directory.CreateDirectory(s_appPath);
        Directory.CreateDirectory(s_appPath2);
    }

    public void Dispose()
    {
        Directory.Delete(s_appPath, true);
        Directory.Delete(s_appPath2, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ParseFromProjectTest()
    {
        var parser = new AppBundleInformationParser(Mock.Of<IMlaunchProcessManager>());

        var info = await parser.ParseFromProject(s_projectFilePath, TestTarget.Simulator_iOS64, "Debug");

        Assert.Equal(AppName, info.AppName);
        Assert.Equal(s_appPath, info.AppPath);
        Assert.Equal(s_appPath, info.LaunchAppPath);
        Assert.Equal(AppName, info.BundleIdentifier);
    }

    [Fact]
    public async Task ParseFromMacCatalystProjectTest()
    {
        var parser = new AppBundleInformationParser(Mock.Of<IMlaunchProcessManager>());

        var info = await parser.ParseFromProject(s_projectFilePath, TestTarget.MacCatalyst, "Debug");

        Assert.Equal(AppName, info.AppName);
        Assert.Equal(s_appPath, info.AppPath);
        Assert.Equal(s_appPath, info.LaunchAppPath);
        Assert.Equal(AppName, info.BundleIdentifier);
        Assert.Equal(Executable, info.BundleExecutable);
    }

    [Fact]
    public async Task ParseFromProjectWithLocatorTest()
    {
        var locator = new Mock<IAppBundleLocator>();
        locator
            .Setup(x => x.LocateAppBundle(It.IsAny<XmlDocument>(), s_projectFilePath, TestTarget.Simulator_iOS64, "Debug"))
            .ReturnsAsync("bin2")
            .Verifiable();

        var parser = new AppBundleInformationParser(Mock.Of<IMlaunchProcessManager>(), locator.Object);

        var info = await parser.ParseFromProject(s_projectFilePath, TestTarget.Simulator_iOS64, "Debug");

        Assert.Equal(AppName, info.AppName);
        Assert.Equal(s_appPath2, info.AppPath);
        Assert.Equal(s_appPath2, info.LaunchAppPath);
        Assert.Equal(AppName, info.BundleIdentifier);

        locator.VerifyAll();
    }
}
