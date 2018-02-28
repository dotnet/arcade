// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.IO.Internal;

namespace Microsoft.DotNet.Build.Tasks.IO
{
    /// <summary>
    /// Computes the checksum for a single file.
    /// </summary>
    public class GetFileHash : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The files to be hashed.
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// The algorithm. Allowed values: SHA256, SHA384, SHA512. Default = SHA256.
        /// </summary>
        public string Algorithm { get; set; } = "SHA256";

        /// <summary>
        /// The metadata name where the hash is store in each item. File hash is in hex. Defaults to "FileHash".
        /// </summary>
        public string MetadataName { get; set; } = "FileHash";

        /// <summary>
        /// The metadata name where the base64 encoded hash is store in each item. File hash is in hex. Defaults to "FileHashBase64".
        /// </summary>
        public string MetadataNameBase64 { get; set; } = "FileHashBase64";

        /// <summary>
        /// The hash of the file in hex. This is only set if there was one item group passed in.
        /// </summary>
        [Output]
        public string Hash { get; set; }

        /// <summary>
        /// The hash of the file base64 encoded. This is only set if there was one item group passed in.
        /// </summary>
        [Output]
        public string HashBase64 { get; set; }

        /// <summary>
        /// The input files with additional metadata set to include the file hash.
        /// </summary>
        [Output]
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            foreach (var file in Files)
            {
                var hash = HashHelper.GetFileHash(Algorithm, file.ItemSpec);
                file.SetMetadata("FileHashAlgoritm", Algorithm);
                file.SetMetadata(MetadataName, HashHelper.ConvertHashToHex(hash));
                file.SetMetadata(MetadataNameBase64, Convert.ToBase64String(hash));
            }

            Items = Files;

            if (Files.Length == 1)
            {
                Hash = Files[0].GetMetadata(MetadataName);
                HashBase64 = Files[0].GetMetadata(MetadataNameBase64);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
