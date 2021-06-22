// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public class GenerateManifestMsi : GenerateTaskBase
    {
        private Version _sdkFeaureBandVersion;

        /// <summary>
        /// The path where the generated MSIs will be placed.
        /// </summary>
        [Required]
        public string OutputPath
        {
            get;
            set;
        }

        /// <summary>
        /// The ID of the workload manifest.
        /// </summary>
        [Required]
        public string ManifestId
        {
            get;
            set;
        }

        /// <summary>
        /// The set of MSIs that were produced.
        /// </summary>
        [Output]
        public ITaskItem[] Msis
        {
            get;
            protected set;
        }

        private Version SdkFeatureBandVersion
        {
            get
            {
                if (_sdkFeaureBandVersion == null)
                {
                    ReleaseVersion sdkReleaseVersion = new ReleaseVersion(SdkVersion);

                    _sdkFeaureBandVersion = new($"{sdkReleaseVersion.Major}.{sdkReleaseVersion.Minor}.{sdkReleaseVersion.SdkFeatureBand}");
                }

                return _sdkFeaureBandVersion;
            }
        }

        /// <summary>
        /// The SDK version, e.g. 6.0.107.
        /// </summary>
        [Required]
        public string SdkVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Semicolon sepearate list of ICEs to suppress.
        /// </summary>
        public string SuppressIces
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the MSI.
        /// </summary>
        [Required]
        public string MsiVersion
        {
            get;
            set;
        }

        [Required]
        public string WorkloadManifestPackage
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage($"Generating workload manifest installer for {SdkFeatureBandVersion}");

                NugetPackage nupkg = new(WorkloadManifestPackage, Log);
                List<TaskItem> msis = new();

                // MSI ProductName defaults to the package title and fallback to the package ID with a warning.
                string productName = nupkg.Title;

                if (string.IsNullOrWhiteSpace(nupkg.Title))
                {
                    Log?.LogMessage(MessageImportance.High, $"'{WorkloadManifestPackage}' should have a non-empty title. The MSI ProductName will be set to the package ID instead.");
                    productName = nupkg.Id;
                }

                // Extract once, but harvest multiple times because some generated attributes are platform dependent. 
                string packageContentsDirectory = Path.Combine(PackageDirectory, $"{nupkg.Identity}");
                nupkg.Extract(packageContentsDirectory, Enumerable.Empty<string>());
                string packageContentsDataDirectory = Path.Combine(packageContentsDirectory, "data");

                foreach (string platform in GenerateMsiBase.SupportedPlatforms)
                {
                    // Extract the MSI template and add it to the list of source files.
                    List<string> sourceFiles = new();
                    string msiSourcePath = Path.Combine(MsiDirectory, $"{nupkg.Id}", $"{nupkg.Version}", platform);
                    sourceFiles.Add(EmbeddedTemplates.Extract("DependencyProvider.wxs", msiSourcePath));
                    sourceFiles.Add(EmbeddedTemplates.Extract("ManifestProduct.wxs", msiSourcePath));

                    string EulaRtfPath = Path.Combine(msiSourcePath, "eula.rtf");
                    File.WriteAllText(EulaRtfPath, GenerateMsiBase.Eula.Replace("__LICENSE_URL__", nupkg.LicenseUrl));
                    EmbeddedTemplates.Extract("Variables.wxi", msiSourcePath);

                    // Harvest the package contents and add it to the source files we need to compile.
                    string packageContentWxs = Path.Combine(msiSourcePath, "PackageContent.wxs");
                    sourceFiles.Add(packageContentWxs);

                    HarvestToolTask heat = new(BuildEngine, WixToolsetPath)
                    {
                        ComponentGroupName = GenerateMsiBase.PackageContentComponentGroupName,
                        DirectoryReference = "ManifestIdDir",
                        OutputFile = packageContentWxs,
                        Platform = platform,
                        SourceDirectory = packageContentsDataDirectory
                    };

                    if (!heat.Execute())
                    {
                        throw new Exception($"Failed to harvest package contents.");
                    }

                    // To support upgrades, the UpgradeCode must be stable withing a feature band.
                    // For example, 6.0.101 and 6.0.108 will generate the same GUID for the same platform.
                    var upgradeCode = Utils.CreateUuid(GenerateMsiBase.UpgradeCodeNamespaceUuid, $"{SdkFeatureBandVersion};{platform}");
                    var productCode = Guid.NewGuid();
                    Log.LogMessage($"UC: {upgradeCode}, PC: {productCode}, {SdkFeatureBandVersion}, {SdkVersion}, {platform}");

                    // Compile the MSI sources
                    string candleIntermediateOutputPath = Path.Combine(IntermediateBaseOutputPath, "wixobj",
                        $"{nupkg.Id}", $"{nupkg.Version}", platform);

                    CompileToolTask candle = new(BuildEngine, WixToolsetPath)
                    {
                        // Candle expects the output path to end with a single '\'
                        OutputPath = candleIntermediateOutputPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        SourceFiles = sourceFiles,
                        Arch = platform
                    };

                    // Configure preprocessor definitions. 
                    string manufacturer = "Microsoft Corporation";

                    if (!string.IsNullOrWhiteSpace(nupkg.Authors) && (nupkg.Authors.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        manufacturer = nupkg.Authors;
                    }
                    Log.LogMessage(MessageImportance.Low, $"Setting Manufacturer to '{manufacturer}'");

                    candle.PreprocessorDefinitions.Add($@"PackageId={nupkg.Id}");
                    candle.PreprocessorDefinitions.Add($@"PackageVersion={nupkg.Version}");
                    candle.PreprocessorDefinitions.Add($@"ProductVersion={MsiVersion}");
                    candle.PreprocessorDefinitions.Add($@"ProductCode={productCode}");
                    candle.PreprocessorDefinitions.Add($@"UpgradeCode={upgradeCode}");
                    // Override the default provider key
                    candle.PreprocessorDefinitions.Add($@"DependencyProviderKey={nupkg.Id},{platform}");
                    candle.PreprocessorDefinitions.Add($@"ProductName={productName}");
                    candle.PreprocessorDefinitions.Add($@"Platform={platform}");
                    candle.PreprocessorDefinitions.Add($@"SourceDir={packageContentsDataDirectory}");
                    candle.PreprocessorDefinitions.Add($@"Manufacturer={manufacturer}");
                    candle.PreprocessorDefinitions.Add($@"EulaRtf={EulaRtfPath}");
                    candle.PreprocessorDefinitions.Add($@"SdkFeatureBandVersion={SdkFeatureBandVersion}");

                    // The temporary installer in the SDK used lower invariants of the manifest ID.
                    // We have to do the same to ensure the keypath generation produces stable GUIDs so that
                    // the manifests/targets get the same component GUIDs.
                    candle.PreprocessorDefinitions.Add($@"ManifestId={ManifestId.ToLowerInvariant()}");

                    // Compiler extension to process dependency provider authoring for package reference counting.
                    candle.Extensions.Add("WixDependencyExtension");

                    if (!candle.Execute())
                    {
                        throw new Exception($"Failed to compile MSI.");
                    }

                    // Link the MSI. The generated filename contains a the semantic version (excluding build metadata) and platform. 
                    // If the source package already contains a platform, e.g. an aliased package that has a RID, then we don't add
                    // the platform again.

                    string shortPackageName = Path.GetFileNameWithoutExtension(WorkloadManifestPackage);

                    string outputFile = Path.Combine(OutputPath, shortPackageName + $"-{platform}.msi");

                    LinkToolTask light = new(BuildEngine, WixToolsetPath)
                    {
                        OutputFile = Path.Combine(OutputPath, outputFile),
                        SourceFiles = Directory.EnumerateFiles(candleIntermediateOutputPath, "*.wixobj"),
                        SuppressIces = this.SuppressIces
                    };

                    // Add WiX extensions
                    light.Extensions.Add("WixDependencyExtension");
                    light.Extensions.Add("WixUIExtension");

                    if (!light.Execute())
                    {
                        throw new Exception($"Failed to link MSI.");
                    }

                    // Generate metadata used for CLI based installations.
                    string msiPath = light.OutputFile;
                    MsiProperties msiProps = new MsiProperties
                    {
                        InstallSize = MsiUtils.GetInstallSize(msiPath),
                        Payload = Path.GetFileName(msiPath),
                        ProductCode = MsiUtils.GetProperty(msiPath, "ProductCode"),
                        ProductVersion = MsiUtils.GetProperty(msiPath, "ProductVersion"),
                        ProviderKeyName = $"{nupkg.Id},{nupkg.Version},{platform}",
                        UpgradeCode = MsiUtils.GetProperty(msiPath, "UpgradeCode")
                    };

                    string msiJsonPath = Path.Combine(Path.GetDirectoryName(msiPath), Path.GetFileNameWithoutExtension(msiPath) + ".json");
                    File.WriteAllText(msiJsonPath, JsonSerializer.Serialize<MsiProperties>(msiProps));

                    TaskItem msi = new(light.OutputFile);
                    msi.SetMetadata(Metadata.Platform, platform);
                    msi.SetMetadata(Metadata.Version, nupkg.ProductVersion);
                    msi.SetMetadata(Metadata.JsonProperties, msiJsonPath);
                    msi.SetMetadata(Metadata.WixObj, candleIntermediateOutputPath);

                    // Generate a .csproj to build a NuGet payload package to carry the MSI and JSON manifest
                    msi.SetMetadata(Metadata.PackageProject, GeneratePackageProject(msi.ItemSpec, msiJsonPath, platform, nupkg));

                    msis.Add(msi);
                }

                Msis = msis.ToArray();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }


        private string GeneratePackageProject(string msiPath, string msiJsonPath, string platform, NugetPackage nupkg)
        {
            string msiPackageProject = Path.Combine(MsiPackageDirectory, platform, nupkg.Id, "msi.csproj");
            string msiPackageProjectDir = Path.GetDirectoryName(msiPackageProject);

            Log?.LogMessage($"Generating package project: '{msiPackageProject}'");

            if (Directory.Exists(msiPackageProjectDir))
            {
                Directory.Delete(msiPackageProjectDir, recursive: true);
            }

            Directory.CreateDirectory(msiPackageProjectDir);

            EmbeddedTemplates.Extract("Icon.png", msiPackageProjectDir);
            EmbeddedTemplates.Extract("LICENSE.TXT", msiPackageProjectDir);

            string licenseTextPath = Path.Combine(msiPackageProjectDir, "LICENSE.TXT");

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
            };

            XmlWriter writer = XmlWriter.Create(msiPackageProject, settings);

            writer.WriteStartElement("Project");
            writer.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");

            writer.WriteStartElement("PropertyGroup");
            writer.WriteElementString("TargetFramework", "net5.0");
            writer.WriteElementString("GeneratePackageOnBuild", "true");
            writer.WriteElementString("IncludeBuildOutput", "false");
            writer.WriteElementString("IsPackable", "true");
            writer.WriteElementString("PackageType", "DotnetPlatform");
            writer.WriteElementString("SuppressDependenciesWhenPacking", "true");
            writer.WriteElementString("NoWarn", "$(NoWarn);NU5128");
            writer.WriteElementString("PackageId", $"{nupkg.Id}.Msi.{platform}");
            writer.WriteElementString("PackageVersion", $"{nupkg.Version}");
            writer.WriteElementString("Description", nupkg.Description);
            writer.WriteElementString("PackageIcon", "Icon.png");

            if (!string.IsNullOrWhiteSpace(nupkg.Authors))
            {
                writer.WriteElementString("Authors", nupkg.Authors);
            }

            if (!string.IsNullOrWhiteSpace(nupkg.Copyright))
            {
                writer.WriteElementString("Copyright", nupkg.Copyright);
            }

            writer.WriteElementString("PackageLicenseExpression", "MIT");
            writer.WriteEndElement();

            writer.WriteStartElement("ItemGroup");
            WriteItem(writer, "None", msiPath, @"\data");
            WriteItem(writer, "None", msiJsonPath, @"\data\msi.json");
            WriteItem(writer, "None", licenseTextPath, @"\");
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.Flush();
            writer.Close();

            return msiPackageProject;
        }

        private void WriteItem(XmlWriter writer, string itemName, string include, string packagePath)
        {
            writer.WriteStartElement(itemName);
            writer.WriteAttributeString("Include", include);
            writer.WriteAttributeString("Pack", "true");
            writer.WriteAttributeString("PackagePath", packagePath);
            writer.WriteEndElement();
        }
    }
}
