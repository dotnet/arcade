// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Windows.Storage;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XUnitRunnerUap
{
    internal static class Helpers
    {
        public static async Task<StorageFile> GetStorageFileAsync(string test)
        {
            var folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync("TestResults", CreationCollisionOption.OpenIfExists);
            return await folder.CreateFileAsync(test, CreationCollisionOption.ReplaceExisting);
        }
    }
}
