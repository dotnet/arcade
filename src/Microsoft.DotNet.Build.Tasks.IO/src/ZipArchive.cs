// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.IO.Internal;
using IOFile = System.IO.File;
using ZipArchiveStream = System.IO.Compression.ZipArchive;

namespace Microsoft.DotNet.Build.Tasks.IO
{
    /// <summary>
    /// Creates a zip archive.
    /// </summary>
    public class ZipArchive : Task
    {
        /// <summary>
        /// The path where the zip file should be created. The containing directory will be created if it doesn't already exist.
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Overwrite <see cref="OutputPath"/> if it exists. Defaults to false.
        /// </summary>
        public bool Overwrite { get; set; }

        //
        // Parameter set 1 - files
        //

        /// <summary>
        /// Files to be added to <see cref="OutputPath"/>.  The `Link` metadata item can be set to explicitly set the zip entry path.
        /// </summary>
        public ITaskItem[] SourceFiles { get; set; }

        /// <summary>
        /// The directory to use as the base directory. The entry path
        /// for each item in <see cref="SourceFiles"/> is relative to this.
        /// </summary>
        public string BaseDirectory { get; set; }

        //
        // Parameter set 2 - directory
        //

        /// <summary>
        /// Creates a zip for an entire directory.
        /// </summary>
        public string SourceDirectory { get; set; }

        /// <summary>
        /// Include the source directory in the zip. Defaults to false.
        /// </summary>
        public bool IncludeSourceDirectory { get; set; } = false;

        public override bool Execute()
        {
            if (IOFile.Exists(OutputPath))
            {
                if (Overwrite)
                {
                    Log.LogMessage(MessageImportance.Low, $"'{OutputPath}' already exists and Overwrite is '{Overwrite}', deleting before zipping...");
                    IOFile.Delete(OutputPath);
                }
                else
                {
                    Log.LogError($"Zip file {OutputPath} already exists. Set {nameof(Overwrite)}=true to replace it.");
                    return !Log.HasLoggedErrors;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            if (!string.IsNullOrEmpty(SourceDirectory))
            {
                return CompressDirectory();
            }
            else
            {
                return CompressFiles();
            }
        }

        private bool CompressDirectory()
        {
            if (SourceFiles?.Length > 0)
            {
                Log.LogError($"{nameof(ZipArchive)} does not support setting both {nameof(SourceDirectory)} and {nameof(SourceFiles)}. Use one or the other, but not both.");
                return false;
            }

            ZipFile.CreateFromDirectory(SourceDirectory, OutputPath, CompressionLevel.Optimal, IncludeSourceDirectory);

            var fileInfo = new FileInfo(OutputPath);
            Log.LogMessage(MessageImportance.High,
                $"Added {SourceDirectory} to '{OutputPath}' ({fileInfo.Length / 1024:n0} KB)");

            return !Log.HasLoggedErrors;
        }

        private bool CompressFiles()
        {
            if (string.IsNullOrEmpty(BaseDirectory))
            {
                Log.LogError($"Missing value for required parameter {nameof(BaseDirectory)}");
                return false;
            }

            var workDir = FileHelpers.EnsureTrailingSlash(BaseDirectory).Replace('\\', '/');

            foreach (var file in SourceFiles)
            {
                if (!string.IsNullOrEmpty(file.GetMetadata("Link")))
                {
                    continue;
                }

                var filePath = file.ItemSpec.Replace('\\', '/');
                if (!filePath.StartsWith(workDir))
                {
                    Log.LogError("Item {0} is not inside the working directory {1}. Set the metadata 'Link' to file path that should be used within the zip archive",
                        filePath,
                        workDir);
                    return false;
                }

                file.SetMetadata("Link", filePath.Substring(workDir.Length));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            using (var stream = IOFile.Create(OutputPath))
            using (var zip = new ZipArchiveStream(stream, ZipArchiveMode.Create))
            {
                foreach (var file in SourceFiles)
                {
                    var entryName = file.GetMetadata("Link").Replace('\\', '/');
                    if (string.IsNullOrEmpty(Path.GetFileName(entryName)))
                    {
                        Log.LogError("Empty file names not allowed. The effective entry path for item '{0}' is '{1}'", file.ItemSpec, entryName);
                        return false;
                    }

                    var entry = zip.CreateEntryFromFile(file.ItemSpec, entryName);
#if NET45
#elif NETCOREAPP2_0
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // This isn't required when creating a zip on Windows. unzip will check which
                        // platform was used to create the zip file. If the zip was created on Windows,
                        // unzip will use a default set of permissions. However, if the zip was created
                        // on a Unix-y system, it will set the permissions as defined in the external_attr
                        // field.

                        // Set the file permissions on each entry so they are extracted correctly on Unix.
                        // Picking -rw-rw-r-- by default because we don't yet have a good way to access existing
                        // Unix permissions. If we don't set this, files may be extracted as ---------- (0000),
                        // which means the files are completely unusable.

                        // FYI - this may not be necessary in future versions of .NET Core. See https://github.com/dotnet/corefx/issues/17342.
                        const int rw_rw_r = (0x8000 + 0x0100 + 0x0080 + 0x0020 + 0x0010 + 0x0004) << 16;
                        entry.ExternalAttributes = rw_rw_r;
                    }
#else
#error Update target frameworks
#endif
                    Log.LogMessage("Added '{0}' to archive", entry.FullName);
                }
            }

            var fileInfo = new FileInfo(OutputPath);
            Log.LogMessage(MessageImportance.High,
                $"Added {SourceFiles.Length} file(s) to '{OutputPath}' ({fileInfo.Length / 1024:n0} KB)");

            return true;
        }
    }
}
