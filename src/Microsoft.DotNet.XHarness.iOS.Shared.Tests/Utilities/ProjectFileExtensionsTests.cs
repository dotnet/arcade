using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Utilities;

public class ProjectFileExtensionsTests
{
    private static XmlDocument CreateDoc(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXmlWithoutNetworkAccess(xml);
        return doc;
    }

    private static XmlDocument GetMSBuildProject(string snippet)
    {
        return CreateDoc($@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=""Build"" ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
{snippet}
</Project>
");
    }

    [Fact]
    public void GetInfoPListNode()
    {
        // Exact Include
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><None Include=\"Info.plist\" /></ItemGroup>")));
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><BundleResource Include=\"Info.plist\" /></ItemGroup>")));
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><Content Include=\"Info.plist\" /></ItemGroup>")));
        Assert.Null(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><Whatever Include=\"Info.plist\" /></ItemGroup>")));

        // With LogicalName
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><None Include=\"doc\"><LogicalName>Info.plist</LogicalName></None></ItemGroup>")));
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><BundleResource Include=\"doc\"><LogicalName>Info.plist</LogicalName></BundleResource></ItemGroup>")));
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><Content Include=\"doc\"><LogicalName>Info.plist</LogicalName></Content></ItemGroup>")));
        Assert.Null(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><Whatever Include=\"Info.plist\"><LogicalName>Info.plist</LogicalName></Whatever></ItemGroup>")));

        // With Link
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><None Include=\"doc\"><Link>Info.plist</Link></None></ItemGroup>")));
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><BundleResource Include=\"doc\"><Link>Info.plist</Link></BundleResource></ItemGroup>")));
        Assert.NotNull(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><Content Include=\"doc\"><Link>Info.plist</Link></Content></ItemGroup>")));
        Assert.Null(ProjectFileExtensions.GetInfoPListNode(GetMSBuildProject("<ItemGroup><Whatever Include=\"Info.plist\"><Link>Info.plist</Link></Whatever></ItemGroup>")));
    }

    [Fact]
    public void MtouchArchPropertyIsDetected()
    {
        var assembly = GetType().Assembly;
        var name = assembly.GetManifestResourceNames().Where(a => a.EndsWith("MtouchArchMissingInConfiguration.xml", StringComparison.Ordinal)).First();
        var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        var csproj = CreateDoc(reader.ReadToEnd());

        var arch = csproj.GetMtouchArch("iPhone", "Release64");

        Assert.Equal("ARM64", arch);
    }

    [Fact]
    public void MissingMtouchArchPropertyInConfigurationIsHandled()
    {
        var assembly = GetType().Assembly;
        var name = assembly.GetManifestResourceNames().Where(a => a.EndsWith("MtouchArchMissingInConfiguration.xml", StringComparison.Ordinal)).First();
        var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        var csproj = CreateDoc(reader.ReadToEnd());

        var arch = csproj.GetMtouchArch("iPhoneSimulator", "Debug");
        Assert.Null(arch);
    }

    [Fact]
    public void MissingMtouchArchPropertyInCsprojIsHandled()
    {
        var assembly = GetType().Assembly;
        var name = assembly.GetManifestResourceNames().Where(a => a.EndsWith("MtouchArchMissingEverywhere.xml", StringComparison.Ordinal)).First();
        var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        var csproj = CreateDoc(reader.ReadToEnd());

        var arch = csproj.GetMtouchArch("iPhoneSimulator", "Debug");
        Assert.Null(arch);
    }
}
