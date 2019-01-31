using System.Collections.Generic;

namespace Microsoft.DotNet.Helix.Client
{
    internal static class Helpers
    {
        public static string RemoveTrailingSlash(string directoryPath)
        {
            return directoryPath.TrimEnd('/', '\\');
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value) { key = pair.Key; value = pair.Value; }
    }
}
