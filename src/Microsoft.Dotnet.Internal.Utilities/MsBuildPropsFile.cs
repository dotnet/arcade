// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

#nullable enable
namespace Microsoft.DotNet.DarcLib.Models;

public interface IMsBuildPropsFile
{
    void SerializeToXml(string path);
}

/// <summary>
/// Represents a regular MSBuild .props file with arbitrary data that can de/serialize to XML.
/// Example:
/// <![CDATA[
///    <Project>
///      <PropertyGroup>
///        -> List of properties comes here <-
///      </PropertyGroup>
///    </Project>
/// ]]>
/// </summary>
public abstract class MsBuildPropsFile : IMsBuildPropsFile
{
    private const string ProjectPropertyName = "Project";
    private const string PropertyGroupName = "PropertyGroup";

    /// <summary>
    /// Controls how we will serialize the properties into the XML.
    ///   - true - ascending
    ///   - false - descending
    ///   - null - keep original order from the dictionary
    /// </summary>
    private readonly bool? _orderPropertiesAscending;

    protected MsBuildPropsFile(bool? orderPropertiesAscending)
    {
        _orderPropertiesAscending = orderPropertiesAscending;
    }

    public void SerializeToXml(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException($"'{path}' is not a valid path.");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        XmlSerializer serializer = new XmlSerializer(typeof(XmlElement));
        var xmlDocument = new XmlDocument();
        XmlElement root = xmlDocument.CreateElement(ProjectPropertyName);
        var propertyGroup = xmlDocument.CreateElement(PropertyGroupName);
        root.AppendChild(propertyGroup);

        SerializeProperties(propertyGroup, xmlDocument.CreateElement);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8,
        };

        using var writer = XmlWriter.Create(path, settings);
        serializer.Serialize(writer, root);
    }

    protected abstract void SerializeProperties(XmlElement propertyGroup, Func<string, XmlElement> createElement);

    protected static Dictionary<string, string> DeserializeProperties(string path) => XDocument.Load(path)
        .Descendants(ProjectPropertyName)
        .Descendants(PropertyGroupName)
        .Descendants()
        .ToDictionary(element => element.Name.LocalName, element => element.Value);

    protected void SerializeDictionary(
        Dictionary<string, string> properties,
        XmlElement propertyGroup,
        Func<string, XmlElement> createElement)
    {
        IEnumerable<string> keys;

        if (_orderPropertiesAscending.HasValue)
        {
            keys = _orderPropertiesAscending.Value
                ? properties.Keys.OrderBy(k => k)
                : properties.Keys.OrderByDescending(k => k);
        }
        else
        {
            keys = properties.Keys;
        }

        foreach (var key in keys)
        {
            var element = createElement(key);
            element.InnerText = properties[key];
            propertyGroup.AppendChild(element);
        }
    }
}

