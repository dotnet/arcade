// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using IOFile = System.IO.File;
using ZipArchiveStream = System.IO.Compression.ZipArchive;

namespace Microsoft.DotNet.Build.Tasks.IO
{
    /// <summary>
    /// Unzips an archive file.
    /// </summary>
    public class UnzipArchive : Task
    {
        /// <summary>
        /// The path to the file to unzip.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// The directory where files will be unzipped. The directory will be created if it does not exist.
        /// </summary>
        [Required]
        public string DestinationFolder { get; set; }

        /// <summary>
        /// Overwrite files if they exists already in <see cref="DestinationFolder"/>. Defaults to false.
        /// </summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>
        /// The files that were unzipped.
        /// </summary>
        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        public override bool Execute()
        {
            if (!IOFile.Exists(File))
            {
                Log.LogError("'{0}' does not exist", File);
                return false;
            }

            Directory.CreateDirectory(DestinationFolder);

            var backslashIsInvalidFileNameChar = Path.GetInvalidFileNameChars().Any(c => c == '\\');

            var output = new List<ITaskItem>();
            using (var stream = IOFile.OpenRead(File))
            using (var zip = new ZipArchiveStream(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    var entryPath = entry.FullName;

                    if (!backslashIsInvalidFileNameChar)
                    {
                        // On non-Windows platforms, a backslash is a valid file name character.
                        // In almost all cases, a backslash in the zip entry was unintentional due
                        // to misuse of ZipArchiveStream. This normalizes backslashes to forward slash
                        // so the backslash is treated as a directory separator.

                        if (entry.FullName.IndexOf('\\') >= 0)
                        {
                            // Normalize backslashes in zip entry.
                            entryPath = entry.FullName.Replace('\\', '/');
                        }
                    }

                    var fileDest = Path.Combine(DestinationFolder, entryPath);
                    var dirName = Path.GetDirectoryName(fileDest);
                    Directory.CreateDirectory(dirName);

                    // Do not try to extract directories
                    if (Path.GetFileName(fileDest) != string.Empty)
                    {
                        entry.ExtractToFile(fileDest, Overwrite);
                        Log.LogMessage(MessageImportance.Low, "Extracted '{0}'", fileDest);
                        output.Add(new TaskItem(fileDest));
                    }
                }
            }

            Log.LogMessage(MessageImportance.High, "Extracted {0} file(s) to '{1}'", output.Count, DestinationFolder);
            OutputFiles = output.ToArray();

            return true;
        }
    }
}
