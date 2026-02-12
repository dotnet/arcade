// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Wix
{
    /// <summary>
    /// Record to track HarvestDirectory item metadata consumed by Heat when generating setup authoring.
    /// </summary>
    /// <param name="Path">The directory to harvest.</param>
    /// <param name="ComponentGroupName">The name of the component group to create for generated authoring.</param>
    /// <param name="DirectoryRefId">The ID of the directory reference to use instead of TARGETDIR.</param>
    /// <param name="PreprocessorVariable">The preprocessor variable to use instead of SourceDir.</param>
    /// <param name="SuppressRegistry">Suppress generation of registry elements.</param>
    /// <param name="SuppressRootDirectory">Suppress generation of a Directory element for the parent directory of the file.</param>
    public record HarvestDirectoryInfo(string Path, string ComponentGroupName, string DirectoryRefId, string PreprocessorVariable,
        bool SuppressRegistry, bool SuppressRootDirectory);

    /// <summary>
    /// Represents an SDK style WiX project.
    /// </summary>
    public class WixProject
    {
        private const string _attributeComponentGroupName = "ComponentGroupName";
        private const string _attributeDirectoryRefId = "DirectoryRefId";
        private const string _attributeInclude = "Include";
        private const string _attributePreprocessorVariable = "PreprocessorVariable";
        private const string _attributeSdk = "Sdk";
        private const string _attributeSuppressRegistry = "SuppressRegistry";
        private const string _attributeSuppressRootDirectory = "SuppressRootDirectory";
        private const string _attributeVersion = "Version";
        private const string _attributeVersionOverride = "VersionOverride";
        private const string _elementPropertyGroup = "PropertyGroup";
        private const string _elementProject = "Project";
        private const string _elementItemGroup = "ItemGroup";
        private const string _itemHarvestDirectory = "HarvestDirectory";
        private const string _itemPackageReference = "PackageReference";
        private const string _propertyDefineConstants = "DefineConstants";

        private const string _defaultSdk = "Microsoft.WixToolset.Sdk";

        private Dictionary<string, string> _packageReferences = new(StringComparer.OrdinalIgnoreCase);

        // Preprocessor variables are case sensitive.
        private Dictionary<string, string> _preprocessorDefinitions = new();

        private Dictionary<string, string> _properties = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, HarvestDirectoryInfo> _harvestDirectories = new(StringComparer.OrdinalIgnoreCase);

        private string _wixToolsetSdk;

        private string _toolsetVersion;

        /// <summary>
        /// Generate VersionOverride attributes for package references.
        /// </summary>
        public bool OverridePackageVersions
        {
            get;
            set;
        }

        /// <summary>
        /// Creates a new <see cref="WixProject"/> instance.
        /// </summary>
        /// <param name="toolsetVersion">The version of the WiX toolset the project will reference.
        /// The version applies to both the toolset SDK and package references.</param>
        /// <param name="wixToolsetSdk">The WiX toolset SDK to use.</param>
        public WixProject(string toolsetVersion, string wixToolsetSdk = _defaultSdk)
        {
            _toolsetVersion = toolsetVersion;
            _wixToolsetSdk = wixToolsetSdk;
        }

        /// <summary>
        /// Generates a .wixproj (XML) file using the specified path and current configuration.
        /// </summary>
        /// <param name="path">The path of the .wixproj to generate.</param>
        public void Save(string path)
        {
            XmlDocument doc = new XmlDocument();
            var project = doc.CreateElement(_elementProject);

            project.SetAttribute(_attributeSdk, $"{_wixToolsetSdk}/{_toolsetVersion}");

            if (_properties.Count > 0)
            {
                var propertyGroup = doc.CreateElement(_elementPropertyGroup);

                foreach (var propertyName in _properties.Keys)
                {
                    var property = doc.CreateElement(propertyName);
                    property.InnerText = _properties[propertyName];
                    propertyGroup.AppendChild(property);
                }

                project.AppendChild(propertyGroup);
            }

            if (_packageReferences.Count > 0)
            {
                var packageReferencesItemGroup = doc.CreateElement(_elementItemGroup);

                foreach (string packageId in _packageReferences.Keys)
                {
                    var item = doc.CreateElement(_itemPackageReference);
                    item.SetAttribute(_attributeInclude, packageId);

                    // Allow null/empty versions in case CPM already defined the packages.
                    if (!string.IsNullOrEmpty(_packageReferences[packageId]))
                    {
                        item.SetAttribute(OverridePackageVersions ? _attributeVersionOverride : _attributeVersion, 
                            _packageReferences[packageId]);
                    }
                    
                    packageReferencesItemGroup.AppendChild(item);
                }

                project.AppendChild(packageReferencesItemGroup);
            }

            if (_preprocessorDefinitions.Count > 0)
            {
                var preprocessorPropertyGroup = doc.CreateElement(_elementPropertyGroup);

                foreach (string key in _preprocessorDefinitions.Keys)
                {
                    var defineConstantsProperty = doc.CreateElement(_propertyDefineConstants);
                    defineConstantsProperty.InnerText = $"$({_propertyDefineConstants});{key}={_preprocessorDefinitions[key]}";
                    preprocessorPropertyGroup.AppendChild(defineConstantsProperty);
                }

                project.AppendChild(preprocessorPropertyGroup);
            }

            if (_harvestDirectories.Count > 0)
            {
                var _harvestDirectoryItemGroup = doc.CreateElement(_elementItemGroup);

                foreach (var harvestInfo in _harvestDirectories.Values)
                {
                    var item = doc.CreateElement(_itemHarvestDirectory);
                    item.SetAttribute(_attributeInclude, harvestInfo.Path);
                    item.SetAttribute(_attributeComponentGroupName, harvestInfo.ComponentGroupName);

                    if (!string.IsNullOrWhiteSpace(harvestInfo.DirectoryRefId))
                    {
                        item.SetAttribute(_attributeDirectoryRefId, harvestInfo.DirectoryRefId);
                    }

                    if (!string.IsNullOrWhiteSpace(harvestInfo.PreprocessorVariable))
                    {
                        item.SetAttribute(_attributePreprocessorVariable, harvestInfo.PreprocessorVariable);
                    }

                    item.SetAttribute(_attributeSuppressRegistry, harvestInfo.SuppressRegistry.ToString().ToLowerInvariant());
                    item.SetAttribute(_attributeSuppressRootDirectory, harvestInfo.SuppressRootDirectory.ToString().ToLowerInvariant());

                    _harvestDirectoryItemGroup.AppendChild(item);
                }

                project.AppendChild(_harvestDirectoryItemGroup);
            }

            // Add the root Project node.
            doc.AppendChild(project);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            using StreamWriter streamWriter = new(path);
            using XmlWriter writer = XmlWriter.Create(streamWriter, settings);
            doc.Save(writer);
        }

        /// <summary>
        /// Adds a package reference using the specified package identifier and version.
        /// </summary>
        /// <param name="id">The package identifier to add.</param>
        /// <param name="version">The version of the package.</param>
        public void AddPackageReference(string id, string version) =>
            _packageReferences[id] = version;

        /// <summary>
        /// Adds a package reference using the specified package identifier and implicit toolset version.
        /// </summary>
        /// <param name="id">The package identifier to add.</param>
        public void AddPackageReference(string id) =>
            AddPackageReference(id, _toolsetVersion);

        /// <summary>
        /// Adds a preprocessor definition using the DefineConstants property.
        /// </summary>
        public void AddPreprocessorDefinition(string name, string value) =>
            _preprocessorDefinitions[name] = value;

        /// <summary>
        /// Adds an msbuild property. 
        /// </summary>
        /// <param name="name">The name of the property to set.</param>
        /// <param name="value">The value of the property to set.</param>
        public void AddProperty(string name, string value) =>
            _properties[name] = value;

        /// <summary>
        /// Adds a directory for harvesting. A package reference for Heat (the harvesting tool) will automatically
        /// be added to the project.
        /// </summary>
        /// <param name="path">The local path of the directory to harvest.</param>
        /// <param name="directoryRefId">The ID of the directory to reference instead of TARGETDIR.</param>
        /// <param name="preprocessorVariable">The preprocessor variable to use instead of SourceDir.</param>
        /// <param name="componentGroupName">The name of the component group to create for generated authoring.</param>
        /// <param name="suppressRegistry">Suppress generation of registry elements.</param>
        /// <param name="suppressRootDirectory">Suppress generation of a Directory element for the parent directory of the file.</param>
        public void AddHarvestDirectory(string path, string directoryRefId = null, string preprocessorVariable = null,
            string componentGroupName = DefaultValues.DefaultComponentGroupName, bool suppressRegistry = true, bool suppressRootDirectory = true)
        {
            _harvestDirectories[path] = new HarvestDirectoryInfo(path, componentGroupName, directoryRefId,
                preprocessorVariable, suppressRegistry, suppressRootDirectory);
            AddPackageReference(ToolsetPackages.MicrosoftWixToolsetHeat, _toolsetVersion);
        }            
    }
}
