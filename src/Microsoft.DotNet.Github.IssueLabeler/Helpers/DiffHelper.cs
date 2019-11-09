// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Github.IssueLabeler.Helpers
{
    public class DiffHelper
    {
        private string[] _fileDiffs;

        /// <summary>
        /// resets file diffs for which we compute filenames, extensions, folders, and subfolder parts
        /// </summary>
        /// <param name="fileDiffs">subset of nested files from a root repository</param>
        public void ResetTo(string[] fileDiffs)
        {
            if (fileDiffs == null || string.IsNullOrEmpty(string.Join(';', fileDiffs)))
            {
                throw new ArgumentNullException(nameof(fileDiffs));
            }
            _fileDiffs = fileDiffs;
            SetupFolderParts();
        }

        /// <summary>
        /// name of files taken from fileDiffs
        /// </summary>
        public IEnumerable<string> Filenames => _fileDiffs.Select(fileWithDiff => Path.GetFileNameWithoutExtension(fileWithDiff));

        /// <summary>
        /// file extensions taken from fileDiffs
        /// </summary>
        public IEnumerable<string> Extensions => _fileDiffs.Select(file => Path.GetExtension(file)).
                Select(extension => string.IsNullOrEmpty(extension) ? "no_extension" : extension);

        /// <summary>
        /// folders taken from parsing the fileDiffs, while keeping track of the number of nested files in each
        /// </summary>
        public Dictionary<string, int> Folders { get; } = new Dictionary<string, int>();

        /// <summary>
        /// folder names taken from parsing the fileDiffs, while keeping track of the number of times such a folder name was repeated
        /// </summary>
        public Dictionary<string, int> FolderNames { get; } = new Dictionary<string, int>();

        private void SetupFolderParts()
        {
            FolderNames.Clear();
            Folders.Clear();
            string folderWithDiff, subfolder;
            string[] folderNames;
            foreach (var fileWithDiff in _fileDiffs)
            {
                folderWithDiff = Path.GetDirectoryName(fileWithDiff) ?? string.Empty;
                folderNames = folderWithDiff.Split(Path.DirectorySeparatorChar);
                subfolder = string.Empty;
                if (!string.IsNullOrEmpty(folderWithDiff))
                {
                    foreach (var folderName in folderNames)
                    {
                        subfolder += folderName;
                        if (FolderNames.ContainsKey(folderName))
                        {
                            FolderNames[folderName] += 1;
                        }
                        else
                        {
                            FolderNames.Add(folderName, 1);
                        }
                        if (Folders.ContainsKey(subfolder))
                        {
                            Folders[subfolder] += 1;
                        }
                        else
                        {
                            Folders.Add(subfolder, 1);
                        }
                        subfolder += Path.DirectorySeparatorChar;
                    }
                }
            }
        }
    }
}
