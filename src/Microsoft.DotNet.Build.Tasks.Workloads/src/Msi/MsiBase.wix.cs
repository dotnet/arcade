// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Base class used for building MSIs.
    /// </summary>
    internal abstract class MsiBase
    {
        /// <summary>
        /// Used to track the number of directories created.
        /// </summary>
        private int _dirCount = 0;

        /// <summary>
        /// The Arcade package that contains the CreateWixBuildWixpack task to support signing.
        /// </summary>
        private const string _MicrosoftDotNetBuildTaskInstallers = "Microsoft.DotNet.Build.Tasks.Installers";

        /// <summary>
        /// Replacement token for license URLs in the generated EULA.
        /// </summary>
        private static readonly string __LICENSE_URL__ = nameof(__LICENSE_URL__);

        /// <summary>
        /// Static RTF text for inserting a EULA into the MSI. The license URL of the NuGet package will be embedded 
        /// as plain text since the text control used to render the MSI UI does not render hyperlinks even though RTF supports
        /// it.
        /// </summary>
        internal static readonly string s_eula = @"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Calibri;}}
{\colortbl ;\red0\green0\blue255;}
{\*\generator Riched20 10.0.19041}\viewkind4\uc1 
\pard\sa200\sl276\slmult1\f0\fs22\lang9 This software is licensed separately as set out in its accompanying license. By continuing, you also agree to that license (__LICENSE_URL__).\par
\par
}";
        /// <summary>
        /// The UUID namespace to use for generating an upgrade code.
        /// </summary>
        internal static readonly Guid UpgradeCodeNamespaceUuid = Guid.Parse("C743F81B-B3B5-4E77-9F6D-474EFF3A722C");

        /// <summary>
        /// Metadata for the MSI such as package ID, version, author information, etc.
        /// </summary>
        public MsiMetadata Metadata
        {
            get;
        }

        /// <summary>
        /// The filename of the MSI. The name excludes the platform identifier and extension.
        /// </summary>
        protected abstract string BaseOutputName
        {
            get;
        }

        protected IBuildEngine BuildEngine
        {
            get;
        }

        /// <summary>
        /// The root intermediate output directory. 
        /// </summary>
        protected string BaseIntermediateOutputPath
        {
            get;
        }

        /// <summary>
        /// When <see langword="true"/>, package references in the generated .wixproj do not include
        /// version information. This is for repos that rely on CPM and building other installers using
        /// SDK style projects.
        /// </summary>
        protected internal bool ManagePackageVersionsCentrally
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the value to use for the manufacturer. 
        /// </summary>
        protected string Manufacturer =>
            (!string.IsNullOrWhiteSpace(Metadata.Authors) && (Metadata.Authors.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) < 0)) ?
            Metadata.Authors :
            DefaultValues.Manufacturer;

        /// <summary>
        /// The platform (bitness) of the MSI.
        /// </summary>
        protected string Platform
        {
            get;
        }

        /// <summary>
        /// The filename of the MSI file to generate.
        /// </summary>
        protected string OutputName => $"{Utils.GetTruncatedHash(BaseOutputName, HashAlgorithmName.SHA256)}-{Platform}.msi";

        /// <summary>
        /// The directory where the WiX source code will be generated.
        /// </summary>
        protected string WixSourceDirectory
        {
            get;
        }

        /// <summary>
        /// Generate VersionOverride attributes for package references. This avoids conflicts when
        /// using CPM and a different version of WiX for non-workload related projects in the same repository.
        /// </summary>
        protected bool OverridePackageVersions
        {
            get;
        }

        /// <summary>
        /// The WiX toolset version. This version applies to both the WiX SDK and any additional toolset
        /// package references for extensions.
        /// </summary>
        protected string WixToolsetVersion
        {
            get;
        }

        /// <summary>
        /// When set to <see langword="true"/>, a wixpack archive will be generated when the MSI is compiled.
        /// The wixpack is used to sign an MSI and its contents when using Arcade.
        /// </summary>
        protected bool GenerateWixpack
        {
            get;
            set;
        }

        /// <summary>
        /// The package version to use when adding package references to the generated .wixproj. Returns <see langword="null""/> 
        /// if <see cref="ManagePackageVersionsCentrally"/> is <see langword="true"/>.
        /// </summary>
        protected string? WixToolsetPackageVersion =>
            ManagePackageVersionsCentrally ? null : WixToolsetVersion;

        /// <summary>
        /// Set of files to include in the NuGet package that will wrap the MSI. Keys represent source files and 
        /// values contain relative paths inside the generated NuGet package.
        /// </summary>
        public Dictionary<string, string> NuGetPackageFiles { get; set; } = new();

        /// <summary>
        /// The output directory to use when generating a wixpack for signing.
        /// </summary>
        public string? WixpackOutputDirectory
        {
            get;
            init;
        }

        /// <summary>
        /// The MSI UpgradeCode.
        /// </summary>
        protected abstract Guid UpgradeCode
        {
            get;
        }

        /// <summary>
        /// The provider key name used to manage MSI dependents.
        /// </summary>
        protected abstract string ProviderKeyName
        {
            get;
        }

        /// <summary>
        /// The name of the registry key for tracking installation records used by the CLI and
        /// and finalizer. May be <see langword="null" /> if the MSI does not support installation
        /// records.
        /// </summary>
        protected abstract string? InstallationRecordKey
        {
            get;
        }

        /// <summary>
        /// The package type represented by the MSI.
        /// </summary>
        protected abstract string? MsiPackageType
        {
            get;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MsiBase"/> class.
        /// </summary>
        /// <param name="metadata">Metadata passed to the <see cref="CreateVisualStudioWorkload"/> task that are used to build the MSI.</param>
        /// <param name="buildEngine"></param>
        /// <param name="platform">The target platform of the MSI.</param>
        /// <param name="baseIntermediateOutputPath">The base directory to use when generating the wix project source files.</param>
        /// <param name="wixToolsetVersion">The version of the WiX toolset to use for building the installer.</param>
        /// <param name="overridePackageVersions">Determines whether PackageOverride attributes should be generated 
        /// when adding package references to avoid CPM conflicts.</param>
        /// <param name="managePackageVersionsCentrally">When set to <see langword="true"/>, package references won't include
        /// package version information, unless version overrides are enabled.</param>
        public MsiBase(MsiMetadata metadata, IBuildEngine buildEngine,
            string platform, string baseIntermediateOutputPath, string wixToolsetVersion = ToolsetInfo.MicrosoftWixToolsetVersion,
            bool overridePackageVersions = false, bool generateWixpack = false,
            string? wixpackOutputDirectory = null, bool managePackageVersionsCentrally = false)
        {
            BuildEngine = buildEngine;
            Platform = platform;
            BaseIntermediateOutputPath = baseIntermediateOutputPath;
            WixToolsetVersion = wixToolsetVersion;
            WixSourceDirectory = Path.Combine(baseIntermediateOutputPath, "src", "wix", Path.GetRandomFileName());
            Metadata = metadata;
            OverridePackageVersions = overridePackageVersions;
            GenerateWixpack = generateWixpack;
            WixpackOutputDirectory = wixpackOutputDirectory;
            ManagePackageVersionsCentrally = managePackageVersionsCentrally;
        }

        /// <summary>
        /// Gets the platform specific ProductName MSI property.  
        /// </summary>
        /// <param name="platform">The platform targeted by the MSI.</param>
        /// <returns>A string containing the product name of the MSI.</returns>
        protected string GetProductName(string platform) =>
            (string.IsNullOrWhiteSpace(Metadata.Title) ? Metadata.Id : Metadata.Title) + $" ({platform})";

        /// <summary>
        /// Generates a EULA (RTF file) that contains the license URL of the underlying NuGet package.
        /// </summary>
        /// <returns>The full path the generated EULA.</returns>
        protected string GenerateEula()
        {
            string eulaRtf = Path.Combine(WixSourceDirectory, "eula.rtf");
            File.WriteAllText(eulaRtf, s_eula.Replace(__LICENSE_URL__, Metadata.LicenseUrl));

            return eulaRtf;
        }

        /// <summary>
        /// Creates a basic WiX project using the specific toolset version and sets common properties and
        /// package references.
        /// </summary>
        /// <param name="toolsetVersion">The WiX toolset version to use for building the project.</param>
        /// <returns>An empty project.</returns>
        /// <remarks>
        /// <para>
        /// The following properties are set: <b>InstallerPlatform, SuppressValidation, OutputType, TargetName,
        /// DebugType</b>
        /// </para>
        /// <para>
        /// The following preprocessor variables are included: <b>InstallerVersion</b>
        /// </para>
        /// </remarks>
        protected virtual WixProject CreateProject()
        {
            if (Directory.Exists(WixSourceDirectory))
            {
                Directory.Delete(WixSourceDirectory, true);
            }

            Directory.CreateDirectory(WixSourceDirectory);

            WixProject wixproj = new(WixToolsetVersion) { OverridePackageVersions = this.OverridePackageVersions };

            // ***********************************************************
            // Initialize common properties and preprocessor definitions.
            // ***********************************************************
            wixproj.AddProperty(WixProperties.InstallerPlatform, Platform);
            // Pacakge is the default in v5, but defaults can change.
            wixproj.AddProperty(WixProperties.OutputType, "Package");
            // Turn off ICE validation. CodeIntegrity and AppLocker block ICE checks that require elevation, even
            // when running as administator. 
            wixproj.AddProperty(WixProperties.SuppressValidation, "true");
            // The WiX SDK will determine the extension based on the output type, e.g. Package -> .msi, Patch -> .msp, etc.
            wixproj.AddProperty(WixProperties.TargetName, Path.GetFileNameWithoutExtension(OutputName));
            // WiX only supports "full". If the property is overridden (Directory.build.props),
            // the compiler will report a warning, e.g. "warning WIX1098: The value 'embedded' is not a valid value for command line argument '-pdbType'. Using the value 'full' instead."
            wixproj.AddProperty(WixProperties.DebugType, "full");
            wixproj.AddProperty("IntermediateOutputPath", @"obj\\$(Configuration)");

            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.Bitness, Platform == "x86" ? "always32" : "always64");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.EulaRtf, GenerateEula());
            // Windows Install 5.0 was released with W2K8 R2 and Windows 7. It's also required to support
            // arm64. See https://learn.microsoft.com/en-us/windows/win32/msi/released-versions-of-windows-installer
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.InstallerVersion, "500");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.Manufacturer, Manufacturer);
            // The package ID and version used to generate the MSI is stored as properties, but
            // has no effect on the MSI. It's only purpose is to capture some information about
            // the source package.
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.PackageId, Metadata.Id);
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.PackageVersion, $"{Metadata.PackageVersion}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.ProductCode, $"{Guid.NewGuid():B}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.ProductLanguage, "1033");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.ProductName, GetProductName(Platform));
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.ProductVersion, $"{Metadata.MsiVersion}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.UpgradeCode, $"{UpgradeCode:B}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.DependencyProviderKeyName, ProviderKeyName);

            if (!string.IsNullOrWhiteSpace(InstallationRecordKey))
            {
                wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.InstallationRecordKey, InstallationRecordKey);
            }

            // All workload MSIs must support reference counting since they are shared between multiple
            // SDKs and Visual Studio.
            wixproj.AddPackageReference(ToolsetPackages.MicrosoftWixToolsetDependencyExtension, WixToolsetPackageVersion);
            // Util extension is required to access the QueryNativeMachine custom action.
            wixproj.AddPackageReference(ToolsetPackages.MicrosoftWixToolsetUtilExtension, WixToolsetPackageVersion);
            // All workload MSIs (manifests or packs) need to override the default dialog set and select a minimal UI.
            wixproj.AddPackageReference(ToolsetPackages.MicrosoftWixToolsetUIExtension, WixToolsetPackageVersion);

            return wixproj;
        }

        /// <summary>
        /// Builds the MSI and returns a task item with metadata about the MSI.
        /// </summary>
        /// <param name="outputPath">The path containing the directory where the MSI will be generated.</param>
        /// <returns>A task item containing metadata related to the MSI.</returns>
        public virtual ITaskItem Build(string outputPath)
        {
            string wixProjectPath = Path.Combine(WixSourceDirectory, "msi.wixproj");
            WixProject wixproj = CreateProject();
            wixproj.AddProperty("OutputPath", outputPath);
            string directoryBuildTargets = EmbeddedTemplates.Extract("Directory.Build.targets", WixSourceDirectory);

            if (GenerateWixpack)
            {
                // Wixpacks need to capture compile time information from the WiX SDK to rebuild the MSI
                // after replacing any unsigned content when using Arcade to sign. 
                Utils.StringReplace(directoryBuildTargets,
                    Encoding.UTF8, ("__WIXPACK_OUTPUT_DIR__", WixpackOutputDirectory));

                // Add a package reference to pull in the CreateWixBuildWixpack task. The version 
                // should automatically default to the "major.minor.path-*", e.g. 10.0.0-*
                wixproj.AddPackageReference(_MicrosoftDotNetBuildTaskInstallers, ToolsetInfo.ArcadeVersion);

                wixproj.AddProperty(WixProperties.GenerateWixpack, "true");
            }

            if (File.Exists(wixProjectPath))
            {
                File.Delete(wixProjectPath);
            }

            wixproj.Save(wixProjectPath);

            // Use DOTNET_HOST_PATH if set, otherwise, fall back to resolivng the host relative to
            // the runtime being used.
            string? dotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrWhiteSpace(dotnetHostPath))
            {
                dotnetHostPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), @"..\..\..\dotnet.exe");
            }

            if (!File.Exists(dotnetHostPath))
            {
                throw new InvalidOperationException("Unable to find a suitable host.");
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = dotnetHostPath,
                Arguments = $"build {wixProjectPath}",
            };

            var buildProcess = Process.Start(startInfo);
            buildProcess?.WaitForExit();

            // Return a task item that contains information about the generated MSI.
            TaskItem msiItem = new TaskItem(Path.Combine(outputPath, OutputName));
            msiItem.SetMetadata(Workloads.Metadata.Platform, Platform);
            msiItem.SetMetadata(Workloads.Metadata.Version, $"{Metadata.MsiVersion}");
            msiItem.SetMetadata(Workloads.Metadata.SwixPackageId, Metadata.SwixPackageId);
            msiItem.SetMetadata(Workloads.Metadata.PackageType, MsiPackageType);

            var fi = new FileInfo(msiItem.ItemSpec);
            if (fi.Length > DefaultValues.MaxMsiSize)
            {
                throw new IOException($"The generated MSI, {msiItem.ItemSpec}, exceeded the maximum size ({DefaultValues.MaxMsiSize} bytes allowed for workloads.)");
            }

            if (GenerateWixpack && !string.IsNullOrEmpty(WixpackOutputDirectory))
            {
                msiItem.SetMetadata(Workloads.Metadata.Wixpack, Path.Combine(
                    WixpackOutputDirectory,
                    Path.GetFileNameWithoutExtension(OutputName)) + ".msi.wixpack.zip");
            }

            AddDefaultPackageFiles(msiItem);

            return msiItem;
        }

        protected void AddDefaultPackageFiles(ITaskItem msi)
        {
            NuGetPackageFiles[msi.GetMetadata(Workloads.Metadata.FullPath)] = @"\data";

            // Create the JSON manifest for CLI based installations.
            string msiJsonPath = MsiProperties.Create(msi.ItemSpec);
            NuGetPackageFiles[Path.GetFullPath(msiJsonPath)] = "\\data\\msi.json";

            NuGetPackageFiles["LICENSE.TXT"] = @"\";
        }

        /// <summary>
        /// Creates a source file containing a directory fragment.
        /// </summary>
        /// <param name="name">The name of the directory.</param>
        /// <param name="id">The ID of the directory.</param>
        /// <param name="reference">The ID of the directory reference (parent directory).</param>

        protected void AddDirectory(string name, string id, string reference)
        {
            try
            {
                AddDirectory(name, id, reference, WixSourceDirectory, $"dir{_dirCount}.wxs");
            }
            finally
            {
                _dirCount++;
            }
        }

        /// <summary>
        /// Creates a source file containing a directory fragment.
        /// </summary>
        /// <param name="name">The name of the directory.</param>
        /// <param name="id">The ID of the directory.</param>
        /// <param name="reference">The ID of the directory reference (parent directory).</param>
        /// <param name="sourceDirectory">The source directory to use for the generated fragment.</param>
        /// <param name="fragmentName">The file name of the generated fragment.</param>
        internal static void AddDirectory(string name, string id, string reference, string sourceDirectory, string fragmentName)
        {
            string dirWxs = EmbeddedTemplates.Extract("DirectoryReference.wxs", sourceDirectory, fragmentName);

            Utils.StringReplace(dirWxs, Encoding.UTF8,
                (MsiTokens.__DIR_REF_ID__, reference), (MsiTokens.__DIR_ID__, id), (MsiTokens.__DIR_NAME__, name));
        }
    }
}

#nullable disable
