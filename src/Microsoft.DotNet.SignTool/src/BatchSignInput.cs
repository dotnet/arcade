// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// Represents all of the input to the batch signing process.
    /// </summary>
    internal sealed class BatchSignInput
    {
        /// <summary>
        /// The ordered names of the files to be signed.
        /// </summary>
        internal ImmutableArray<FileSignInfo> FilesToSign { get; }

        /// <summary>
        /// Holds information about each of the containers that will be signed.
        /// The key is the content hash of the file.
        /// </summary>
        internal ImmutableDictionary<ImmutableArray<byte>, ZipData> ZipDataMap;

        /// <summary>
        /// A list of files whose content needs to be overwritten by signed content from a different file.
        /// Copy the content of file with full path specified in Key to file with full path specified in Value.
        /// </summary>
        internal ImmutableArray<KeyValuePair<string, string>> FilesToCopy;

        internal List<FileSignInfo> ZipFilesToRepack;

        internal BatchSignInput(ImmutableArray<FileSignInfo> filesToSign, ImmutableDictionary<ImmutableArray<byte>, ZipData> zipDataMap, 
            ImmutableArray<KeyValuePair<string, string>> filesToCopy, List<FileSignInfo> zipFilesToRepack)
        {
            Debug.Assert(!filesToSign.IsDefault);
            Debug.Assert(zipDataMap != null);
            Debug.Assert(zipDataMap.KeyComparer == ByteSequenceComparer.Instance);
            Debug.Assert(!filesToCopy.IsDefault);

            FilesToSign = filesToSign;
            ZipDataMap = zipDataMap;
            FilesToCopy = filesToCopy;
            ZipFilesToRepack = zipFilesToRepack;
        }
    }
}
