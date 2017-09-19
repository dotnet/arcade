using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace XUnit.Runner.Uap
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
