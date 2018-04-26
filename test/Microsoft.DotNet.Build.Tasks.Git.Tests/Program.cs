using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Git.Tests
{
    public class Program
    {
        [Fact]
        static void TestOne()
        {
            var task = new GitBranch
            {
                BuildEngine = new IO.Tests.MockEngine()
            };

            Assert.True(task.Execute());
        }
    }
}
