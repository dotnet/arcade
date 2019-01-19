using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal struct DirectoryLock : IDisposable
    {
        public static string LockFileExtension { get; } = ".dir.lock";

        public static async Task<DirectoryLock> AcquireAsync(string path)
        {
            var l = new DirectoryLock(path);
            await l.AcquireAsync().ConfigureAwait(false);
            return l;
        }

        public string DirectoryPath { get; }

        private string LockFile => DirectoryPath + LockFileExtension;
        private FileStream LockedFile { get; set; }

        private DirectoryLock(string directoryPath)
        {
            if (directoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            DirectoryPath = Helpers.RemoveTrailingSlash(directoryPath);
            LockedFile = null;
        }

        private async Task AcquireAsync()
        {
            FileStream stream;
            Console.WriteLine($"Locking Directory {DirectoryPath}");
            while (true)
            {
                try
                {
                    stream = new FileStream(
                        LockFile,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.Delete,
                        4096,
                        FileOptions.DeleteOnClose);
                    break;
                }
                catch (IOException ex)
                {
                    if (
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (uint)ex.HResult == 0x80070050) || // ERROR_FILE_EXISTS
                        ((uint)ex.HResult == 17) // EEXIST
                        )
                    {
                        await Task.Delay(5000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            LockedFile = stream;
        }

        public void Dispose()
        {
            Console.WriteLine($"Unlocking Directory {DirectoryPath}");
            LockedFile?.Dispose();
        }
    }
}
