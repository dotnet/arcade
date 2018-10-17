// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    /// <summary>
    ///     This is the only model currently provided directly by DarcLib. The reason is that there is a bit of
    ///     a circular dependency with respect to data between Maestro and DarcLib. DarcLib relies on the
    ///     generated BAR client, which provides models including AssetData.  It provides APIs that take AssetData.
    ///     However, Maestro itself interacts with these DarcLib APIs. Since it is defining what the generated models
    ///     in the BAR client look like, it shouldn't reference the client itself.  So we introduce an extra DarcLib
    ///     specific model for AssetData.
    /// </summary>
    public class AssetData
    {
        public string Name { get; set; }

        public string Version { get; set; }
    }
}
