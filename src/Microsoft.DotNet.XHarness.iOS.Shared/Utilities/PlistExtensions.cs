// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

public static class PListExtensions
{
    private static readonly ConditionalWeakTable<XmlDocument, string> s_filenames = new();

    public const string BundleExecutablePropertyName = "CFBundleExecutable";
    public const string BundleIdentifierPropertyName = "CFBundleIdentifier";
    public const string BundleNamePropertyName = "CFBundleName";
    public const string RequiredDeviceCapabilities = "UIRequiredDeviceCapabilities";

    public static string GetFilename(this XmlDocument doc)
    {
        s_filenames.TryGetValue(doc, out var rv);
        return rv;
    }

    public static void LoadWithoutNetworkAccess(this XmlDocument doc, string filename)
    {
        using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
        {
            var settings = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Parse,
            };
            using (var reader = XmlReader.Create(fs, settings))
            {
                doc.Load(reader);
            }
        }
        s_filenames.Add(doc, filename);
    }

    public static void LoadXmlWithoutNetworkAccess(this XmlDocument doc, string xml)
    {
        using (var fs = new StringReader(xml))
        {
            var settings = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Parse,
            };
            using (var reader = XmlReader.Create(fs, settings))
            {
                doc.Load(reader);
            }
        }
    }

    public static void SetMinimumOSVersion(this XmlDocument plist, string value) =>
        plist.SetPListStringValue("MinimumOSVersion", value);

    public static void SetMinimummacOSVersion(this XmlDocument plist, string value) =>
        plist.SetPListStringValue("LSMinimumSystemVersion", value);

    public static void SetCFBundleDisplayName(this XmlDocument plist, string value) =>
        plist.SetPListStringValue("CFBundleDisplayName", value);

    public static string GetCFBundleDisplayName(this XmlDocument plist) =>
        plist.GetPListStringValue("CFBundleDisplayName");

    public static string GetCFBundleExecutable(this XmlDocument plist)
    {
        TryGetPListStringValue(plist, BundleExecutablePropertyName, out var value);
        return value;
    }

    public static string GetMinimumOSVersion(this XmlDocument plist) =>
        plist.GetPListStringValue("MinimumOSVersion");

    public static string GetMinimummacOSVersion(this XmlDocument plist) =>
        plist.GetPListStringValue("LSMinimumSystemVersion");

    public static void SetCFBundleIdentifier(this XmlDocument plist, string value) =>
        plist.SetPListStringValue(BundleIdentifierPropertyName, value);

    public static void SetCFBundleName(this XmlDocument plist, string value) =>
        plist.SetPListStringValue(BundleNamePropertyName, value);

    public static void SetUIDeviceFamily(this XmlDocument plist, params int[] families) =>
        plist.SetPListArrayOfIntegerValues("UIDeviceFamily", families);

    public static string GetCFBundleIdentifier(this XmlDocument plist) =>
        plist.GetPListStringValue(BundleIdentifierPropertyName);

    public static string GetCFBundleName(this XmlDocument plist) =>
        plist.GetPListStringValue(BundleNamePropertyName);

    public static string GetNSExtensionPointIdentifier(this XmlDocument plist)
    {
        TryGetPListStringValue(plist, "NSExtensionPointIdentifier", out var value);
        return value;
    }

    public static void SetPListStringValue(this XmlDocument plist, string node, string value)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var element = plist.SelectSingleNode("//dict/key[text()='" + node + "']");
        if (element == null)
        {
            plist.AddPListStringValue(node, value);
        }
        else
        {
            element.NextSibling.InnerText = value;
        }
    }

    public static void AddPListStringValue(this XmlDocument plist, string node, string value)
    {
        var keyElement = plist.CreateElement("key");
        keyElement.InnerText = node;
        var valueElement = plist.CreateElement("string");
        valueElement.InnerText = value;
        var root = plist.SelectSingleNode("//dict");
        root.AppendChild(keyElement);
        root.AppendChild(valueElement);
    }

    public static void AddPListKeyValuePair(this XmlDocument plist, string node, string valueType, string value)
    {
        var keyElement = plist.CreateElement("key");
        keyElement.InnerText = node;
        var valueElement = plist.CreateElement(valueType);
        valueElement.InnerXml = value;
        var root = plist.SelectSingleNode("//dict");
        root.AppendChild(keyElement);
        root.AppendChild(valueElement);
    }

    public static bool ContainsKey(this XmlDocument plist, string key) =>
        TryGetPListStringValue(plist, key, out _);

    private static void SetPListArrayOfIntegerValues(this XmlDocument plist, string node, params int[] values)
    {
        var key = plist.SelectSingleNode("//dict/key[text()='" + node + "']");
        key.ParentNode.RemoveChild(key.NextSibling);
        var array = plist.CreateElement("array");
        foreach (var value in values)
        {
            var element = plist.CreateElement("integer");
            element.InnerText = value.ToString();
            array.AppendChild(element);
        }
        key.ParentNode.InsertAfter(array, key);
    }

    private static string GetPListStringValue(this XmlDocument plist, string node) =>
        plist.SelectSingleNode("//dict/key[text()='" + node + "']").NextSibling.InnerText;

    private static bool TryGetPListStringValue(this XmlDocument plist, string nodeName, out string value)
    {
        var node = plist.SelectSingleNode("//dict/key[text()='" + nodeName + "']")?.NextSibling;
        if (node == null)
        {
            value = null;
            return false;
        }

        value = node.InnerText;
        return true;
    }
}
