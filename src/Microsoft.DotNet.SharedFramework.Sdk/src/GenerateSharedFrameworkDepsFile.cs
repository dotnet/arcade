// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.SharedFramework.Sdk
{
    public class GenerateSharedFrameworkDepsFile : BuildTask
    {
        [Required]
        public string TargetFrameworkMoniker { get; set; }

        [Required]
        public string RuntimeIdentifier { get; set; }

        [Required]
        public string SharedFrameworkName { get; set; }

        [Required]
        public string SharedFrameworkPackName { get; set; }

        [Required]
        public string Version { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        public string SharedFrameworkDepsNameOverride { get; set; }

        [Output]
        public ITaskItem GeneratedDepsFile { get; set; }

        public override bool Execute()
        {
            var target = new TargetInfo(TargetFrameworkMoniker, RuntimeIdentifier, string.Empty, isPortable: false);
            var runtimeFiles = new List<RuntimeFile>();
            var nativeFiles = new List<RuntimeFile>();
            var resourceAssemblies = new List<ResourceAssembly>();

            foreach (var file in Files)
            {
                var filePath = file.ItemSpec;
                var fileName = Path.GetFileName(filePath);
                var fileVersion = FileUtilities.GetFileVersion(filePath)?.ToString() ?? string.Empty;
                var assemblyVersion = FileUtilities.GetAssemblyName(filePath)?.Version;
                string cultureMaybe = file.GetMetadata("Culture");
                if (!string.IsNullOrEmpty(cultureMaybe))
                {
                    resourceAssemblies.Add(new ResourceAssembly(Path.Combine(cultureMaybe, fileName), cultureMaybe));
                }
                else if (assemblyVersion == null)
                {
                    var nativeFile = new RuntimeFile(fileName, null, fileVersion);
                    nativeFiles.Add(nativeFile);
                }
                else if (string.IsNullOrEmpty(file.GetMetadata("GeneratedBuildFile")))
                {
                    var runtimeFile = new RuntimeFile(fileName,
                        fileVersion: fileVersion,
                        assemblyVersion: assemblyVersion.ToString());
                    runtimeFiles.Add(runtimeFile);
                }
            }

            var runtimeLibrary = new RuntimeLibrary("package",
               SharedFrameworkPackName,
               Version,
               hash: string.Empty,
               runtimeAssemblyGroups: new[] { new RuntimeAssetGroup(string.Empty, runtimeFiles) },
               nativeLibraryGroups: new[] { new RuntimeAssetGroup(string.Empty, nativeFiles) },
               resourceAssemblies,
               Array.Empty<Dependency>(),
               hashPath: null,
               path: $"{SharedFrameworkPackName.ToLowerInvariant()}/{Version}",
               serviceable: true);

            var context = new DependencyContext(target,
                CompilationOptions.Default,
                Enumerable.Empty<CompilationLibrary>(),
                new[] { runtimeLibrary },
                Enumerable.Empty<RuntimeFallbacks>());

            var depsFileName = string.IsNullOrEmpty(SharedFrameworkDepsNameOverride) ? $"{SharedFrameworkName}.deps.json" : $"{SharedFrameworkDepsNameOverride}.deps.json";

            var depsFilePath = Path.Combine(IntermediateOutputPath, depsFileName);
            try
            {
                using var depsStream = File.Create(depsFilePath);
                new DependencyContextWriter().Write(context, depsStream);
                GeneratedDepsFile = new TaskItem(depsFilePath);
            }
            catch (Exception ex)
            {
                // If there is a problem, ensure we don't write a partially complete version to disk.
                if (File.Exists(depsFilePath))
                {
                    File.Delete(depsFilePath);
                }
                Log.LogErrorFromException(ex, false);
                return false;
            }
            return true;
        }
    }
}
