using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Arcade.Common
{
    public class Helpers : IHelpers
    {
        public string RemoveTrailingSlash(string directoryPath)
        {
            return directoryPath.TrimEnd('/', '\\');
        }

        public string ComputeSha256Hash(string normalizedPath)
        {
            using (var hasher = SHA256.Create())
            {
                var dirHash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath)));
                dirHash = dirHash.TrimEnd('='); // base64 url encode it.
                dirHash = dirHash.Replace('+', '-');
                dirHash = dirHash.Replace('/', '_');
                return dirHash;
            }
        }

        public T MutexExec<T>(Func<T> function, string mutexName)
        {
            using var mutex = new Mutex(false, mutexName);
            bool hasMutex = false;

            try
            {
                try
                {
                    mutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                }

                hasMutex = true;
                return function();
            }
            finally
            {
                if (hasMutex)
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        public T DirectoryMutexExec<T>(Func<T> function, string path) =>
            MutexExec(
                function,
                $"Global\\{ComputeSha256Hash(path)}");

        public T MutexExec<T>(Func<Task<T>> function, string mutexName) =>
            MutexExec(() => function().GetAwaiter().GetResult(), mutexName); // Can't await because of mutex

        public T DirectoryMutexExec<T>(Func<Task<T>> function, string path) =>
            DirectoryMutexExec(() => function().GetAwaiter().GetResult(), path); // Can't await because of mutex        
    }

    public static class KeyValuePairExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value) { key = pair.Key; value = pair.Value; }
    }
}
