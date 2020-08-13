using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public static class TestInputs
    {
        public static string GetFullPath(string testInputName)
        {
            return Path.Combine(
                Path.GetDirectoryName(typeof(TestInputs).Assembly.Location),
                "TestInputs",
                testInputName);
        }

        public static byte[] ReadAllBytes(string testInputName)
        {
            var path = GetFullPath(testInputName);
            return File.ReadAllBytes(path);
        }
    }
}
