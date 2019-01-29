namespace Microsoft.DotNet.Helix.Client
{
    internal static class Helpers
    {
        public static string RemoveTrailingSlash(string directoryPath)
        {
            return directoryPath.TrimEnd('/', '\\');
        }
    }
}