// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// Represents all of the input to the batch signing process.
    /// </summary>
    internal sealed class BatchSignInput
    {
        /// <summary>
        /// Uri, to be consumed by later steps, which describes where these files get published to.
        /// </summary>
        internal string PublishUri { get; }

        /// <summary>
        /// The ordered names of the files to be signed.
        /// </summary>
        internal ImmutableArray<FileSignInfo> FilesToSign { get; }

        /// <summary>
        /// Holds information about each of the containers that will be signed.
        /// The key is a full file path.
        /// </summary>
        internal ImmutableDictionary<string, ZipData> ZipDataMap;

        internal BatchSignInput(ImmutableList<FileSignInfo> fileSignDataMap, ImmutableDictionary<string, ZipData> zipDataMap ,string publishUri)
        {
            // Use order by to make the output of this tool as predictable as possible.
            FilesToSign = fileSignDataMap.OrderBy(x => x.FullPath).ToImmutableArray();

            ZipDataMap = zipDataMap;
            PublishUri = publishUri;
        }
    }
}
