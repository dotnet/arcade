// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.IO.Internal;

namespace Microsoft.DotNet.Build.Tasks.IO
{
    /// <summary>
    /// Verifies that a file matches the expected file hash.
    /// </summary>
    public class VerifyFileHash : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The file path.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// The algorithm. Allowed values: SHA256, SHA384, SHA512.
        /// </summary>
        public string Algorithm { get; set; } = "SHA256";

        /// <summary>
        /// The expected hash of the file in hex.
        /// </summary>
        [Required]
        public string Hash { get; set; }

        public override bool Execute()
        {
            var hash = HashHelper.GetFileHash(Algorithm, File);
            var actualHash = HashHelper.ConvertHashToHex(hash);

            if (!string.Equals(actualHash, Hash, StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError($"Checksum mismatch. Expected {File} to have {Algorithm} checksum of {Hash}, but it was {actualHash}");
                return false;
            }

            return true;
        }
    }
}
