using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Utilities;

public class PListExtensionsTests
{
    private readonly XmlDocument _plist;

    public PListExtensionsTests() =>
        _plist = CreateResultSample();

    /// <summary>
    /// Creates a sample pList to be used with the tests and returns the temp file in which it was stored.
    /// </summary>
    /// <returns>The path where the sample plist can be found.</returns>
    private XmlDocument CreateResultSample()
    {
        var name = GetType().Assembly.GetManifestResourceNames()
            .FirstOrDefault(a => a.EndsWith("Info.plist", StringComparison.Ordinal));
        var tempPath = Path.GetTempFileName();
        var byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble()); // I hate BOM
        using var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name));

        // create the document with the plist and return it
        var doc = new XmlDocument();
        var settings = new XmlReaderSettings()
        {
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Parse,
        };
        using var reader = XmlReader.Create(sampleStream, settings);
        doc.Load(reader);
        return doc;
    }

    [Fact]
    public void SetMinimumOSVersion()
    {
        var version = "MyMinVersion";
        _plist.SetMinimumOSVersion(version);
        Assert.Equal(version, _plist.GetMinimumOSVersion());
    }

    [Fact]
    public void SetNullMinimumOSVersion() => Assert.Throws<ArgumentNullException>(() => _plist.SetMinimumOSVersion(null));

    [Fact]
    public void SetMinimummacOSVersion()
    {
        var version = "MyMaccMinVersion";
        _plist.SetMinimummacOSVersion(version);
        Assert.Equal(version, _plist.GetMinimummacOSVersion());
    }

    [Fact]
    public void SetNullMinimummacOSVersion() => Assert.Throws<ArgumentNullException>(() => _plist.SetMinimummacOSVersion(null));

    [Fact]
    public void SetCFBundleDisplayName()
    {
        var displayName = "MySuperApp";
        _plist.SetCFBundleDisplayName(displayName);
        Assert.Equal(displayName, _plist.GetCFBundleDisplayName());
    }

    [Fact]
    public void SetNullCFBundleDisplayName() => Assert.Throws<ArgumentNullException>(() => _plist.SetCFBundleDisplayName(null));

    [Fact]
    public void SetCFBundleIdentifier()
    {
        var bundleIdentifier = "my.company.super.app";
        _plist.SetCFBundleIdentifier(bundleIdentifier);
        Assert.Equal(bundleIdentifier, _plist.GetCFBundleIdentifier());
    }

    [Fact]
    public void SetNullCFBundleIdentifier() => Assert.Throws<ArgumentNullException>(() => _plist.SetCFBundleIdentifier(null));

    [Fact]
    public void SetCFBundleName()
    {
        var bundleName = "MySuper.app";
        _plist.SetCFBundleName(bundleName);
        Assert.Equal(bundleName, _plist.GetCFBundleName());
    }

    [Fact]
    public void SetNullCFBundleName() => Assert.Throws<ArgumentNullException>(() => _plist.SetCFBundleName(null));
}
