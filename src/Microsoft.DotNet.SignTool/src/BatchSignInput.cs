// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        internal BatchSignInput(ImmutableArray<FileSignInfo> filesToSign, ImmutableDictionary<ImmutableArray<byte>, ZipData> zipDataMap)
        {
            Debug.Assert(!filesToSign.IsDefault);
            Debug.Assert(zipDataMap != null);

            FilesToSign = filesToSign;
            ZipDataMap = zipDataMap;
        }
    }
}
