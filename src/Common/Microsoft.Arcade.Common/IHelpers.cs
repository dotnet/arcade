using System;
using System.Threading.Tasks;

namespace Microsoft.Arcade.Common
{
    public interface IHelpers
    {
        string ComputeSha256Hash(string normalizedPath);
        T DirectoryMutexExec<T>(Func<T> function, string path);
        T DirectoryMutexExec<T>(Func<Task<T>> function, string path);
        T MutexExec<T>(Func<T> function, string mutexName);
        T MutexExec<T>(Func<Task<T>> function, string mutexName);
        string RemoveTrailingSlash(string directoryPath);
    }
}