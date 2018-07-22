using System;
using System.Collections.Generic;
using System.IO;
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

        public static async Task<StreamWriter> GetFileStreamWriterInLocalStorageAsync(string fileName)
        {
            var localFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("AC", CreationCollisionOption.OpenIfExists);
            var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            return new StreamWriter(await file.OpenStreamForWriteAsync())
            {
                AutoFlush = true
            };
        }

    }
}
