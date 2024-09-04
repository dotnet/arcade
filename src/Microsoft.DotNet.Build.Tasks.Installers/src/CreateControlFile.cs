// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.Installers
{
    public sealed class CreateControlFile : BuildTask
    {
        [Required]
        public string PackageName { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        [Required]
        public string PackageArchitecture { get; set; }

        [Required]
        public string Maintainer { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string InstalledSize { get; set; }

        [Required]
        public ITaskItem[] Depends { get; set; }

        [Required]
        public string Section { get; set; }

        public ITaskItem[] AdditionalProperties { get; set; }

        [Required]
        public string ControlFileOutputPath { get; set; }

        public override bool Execute()
        {
            using Stream stream = File.OpenWrite(ControlFileOutputPath);
            using StreamWriter writer = new(stream, Encoding.ASCII);
            writer.WriteLine($"Package: {PackageName}");
            writer.WriteLine($"Version: {PackageVersion}");
            writer.WriteLine($"Architecture: {PackageArchitecture}");
            writer.WriteLine($"Maintainer: {Maintainer}");
            writer.WriteLine($"Installed-Size: {InstalledSize}");
            writer.WriteLine("Depends: " + string.Join(", ", Depends.Select(d => d.ItemSpec + d.GetMetadata("Version") is { } version ? $" (>= {d.GetMetadata("Version")})": "")));
            writer.WriteLine($"Section: {Section}");
            writer.WriteLine("Priority: standard");
            writer.WriteLine("Homepage: https://github.com/dotnet/core");
            foreach (ITaskItem property in AdditionalProperties)
            {
                writer.WriteLine($"{property.ItemSpec}: {property.GetMetadata("Value")}");
            }
            writer.WriteLine($"Description: {Description}");
            return true;
        }
    }
}
