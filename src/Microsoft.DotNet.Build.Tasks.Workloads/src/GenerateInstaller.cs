// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// MSBuild task to generate a pack installer (MSI) for use in a .NET workload.
    /// </summary>
    public class GenerateInstaller : Task
    {
        internal const string PackageContentComponentGroupName = "CG_PackageContent";
        internal const string PackageContentDirectoryReference = "VersionDir";

        /// <summary>
        /// Wildcard patterns of files that should be removed from the extract package prior to harvesting its content.
        /// </summary>
        public ITaskItem[] ExcludeFiles
        {
            get;
            set;
        } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The root installation directory, relative to DOTNET_HOME.
        /// </summary>
        [Required]
        public string InstallDir
        {
            get;
            set;
        } = "packs";

        /// <summary>
        /// The intermediate output path to use during compilation and linking.
        /// </summary>
        public string IntermediateOutputPath
        {
            get;
            set;
        }

        [Output]
        public string Msi
        {
            get;
            private set;
        }

        /// <summary>
        /// The file name and extension of the MSI. 
        /// </summary>
        public string OutputFile
        {
            get;
            set;
        }

        /// <summary>
        /// The path where the generated MSI will be placed.
        /// </summary>
        public string OutputPath
        {
            get;
            set;
        }

        /// <summary>
        /// The target platform of the MSI
        /// </summary>
        public string Platform
        {
            get;
            set;
        } = "x64";

        /// <summary>
        /// The display name of the MSI.
        /// </summary>
        public string ProductName
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the NuGet package to harvest.
        /// </summary>
        [Required]
        public string SourcePackage
        {
            get;
            set;
        }

        [Required]
        public string WixToolsetPath
        {
            get;
            set;
        }

        internal string PackageContentsDir
        {
            get;
            private set;
        }

        public override bool Execute()
        {
            try
            {
                EmbeddedTemplates.Log = Log;
                // Extract the package.
                NugetPackage package = new(SourcePackage, Log);
                string intermediate = Path.Combine(IntermediateOutputPath, package.Identity.ToString());
                PackageContentsDir = Path.Combine(intermediate, "a");

                Log.LogMessage($"Exclude Files: {ExcludeFiles?.Length}");

                IEnumerable<string> exclusions = ExcludeFiles is null ? Enumerable.Empty<string>() :
                    from e in ExcludeFiles
                    select Utils.ConvertToRegexPattern(e.ItemSpec);

                package.Extract(PackageContentsDir, exclusions);
                Log.LogMessage(MessageImportance.Low, $"Extracting '{SourcePackage}' to '{PackageContentsDir}'");

                // Extract the MSI template and add it to the list of source files.
                List<string> sourceFiles = new();
                string templateSourcePath = Path.Combine(intermediate, "b");
                sourceFiles.Add(EmbeddedTemplates.Extract("DependencyProvider.wxs", templateSourcePath));
                sourceFiles.Add(EmbeddedTemplates.Extract("Directories.wxs", templateSourcePath));
                sourceFiles.Add(EmbeddedTemplates.Extract("Product.wxs", templateSourcePath));
                sourceFiles.Add(EmbeddedTemplates.Extract("Registry.wxs", templateSourcePath));
                EmbeddedTemplates.Extract("Variables.wxi", templateSourcePath);

                // Harvest the package contents and add it to the source files we need to compile.
                string packageContentWxs = Path.Combine(templateSourcePath, "PackageContent.wxs");
                sourceFiles.Add(packageContentWxs);

                HarvestToolTask heat = new(BuildEngine, WixToolsetPath)
                {
                    ComponentGroupName = PackageContentComponentGroupName,
                    DirectoryReference = PackageContentDirectoryReference,
                    OutputFile = packageContentWxs,
                    Platform = this.Platform,
                    SourceDirectory = PackageContentsDir
                };

                if (!heat.Execute())
                {
                    return false;
                }

                // Compile the MSI sources
                string candleIntermediateOutputPath = Path.Combine(intermediate, "c");

                CompileToolTask candle = new(BuildEngine, WixToolsetPath)
                {
                    // Candle expects the output path to end with a single '\'
                    OutputPath = candleIntermediateOutputPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    SourceFiles = sourceFiles,
                    Arch = Platform
                };

                // Configure preprocessor definitions
                candle.PreprocessorDefinitions.AddRange(package.GetPreprocessorDefinitions());
                candle.PreprocessorDefinitions.Add($@"InstallDir={InstallDir}");
                candle.PreprocessorDefinitions.Add($@"ProductName={ProductName}");
                candle.PreprocessorDefinitions.Add($@"Platform={Platform}");
                candle.PreprocessorDefinitions.Add($@"SourceDir={PackageContentsDir}");

                // Compiler extension to process dependency provider authoring for package reference counting.
                candle.Extensions.Add("WixDependencyExtension");

                if (!candle.Execute())
                {
                    return false;
                }

                // Link 
                LinkToolTask light = new(BuildEngine, WixToolsetPath)
                {
                    OutputFile = Path.Combine(OutputPath, OutputFile),
                    SourceFiles = Directory.EnumerateFiles(candleIntermediateOutputPath, "*.wixobj")
                };

                // Add extensions
                light.Extensions.Add("WixDependencyExtension");
                light.Extensions.Add("WixUIExtension");

                if (!light.Execute())
                {
                    return false;
                }

                Msi = light.OutputFile;
            }
            catch (Exception e)
            {
                Log.LogMessage(e.Message);
                Log.LogMessage(e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
