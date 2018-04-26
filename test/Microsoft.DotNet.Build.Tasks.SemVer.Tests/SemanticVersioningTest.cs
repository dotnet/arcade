using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.SemVer.Tests
{
    public class SemanticVersioningTest
    {
        public static IEnumerable<object[]> GetTestSuccessCases()
        {
            yield return new object[] { 1, 2, 0, "preview1", 8530, 0, "asdf34234", "1.2.0-preview1.08530.0+asdf34234" };
            yield return new object[] { 3, 0, 1, "beta2", 26405, 10, "asd34523", "3.0.1-beta2.26405.10+asd34523" };

            yield return new object[] { 1, 2, 0, "preview1", 0, 0, String.Empty, "1.2.0-preview1 (stabilized)" };
            yield return new object[] { 3, 0, 1, String.Empty, 0, 0, String.Empty, "3.0.1 (stabilized)" };
        }

        public static IEnumerable<object[]> GetTestFailCases()
        {
            // Shouldn't accept 0 Major version
            yield return new object[] { 0, 0, 0, String.Empty, 0, 0, String.Empty };
            yield return new object[] { 0, 1, 1, "Microsoft", 1, 1, ".NET" };

            // If prerelease is empty all other prerelease fields also should be
            yield return new object[] { 1, 2, 3, String.Empty, 1, 1, "Arcade" };
        }

        [Theory]
        [MemberData(nameof(GetTestSuccessCases))]
        public void ExpectToPassTests(Int16 Major, Int16 Minor, Int16 Patch, string Prerelease, Int16 ShortDate, Int16 Builds, string sha, string ExpectedOutput)
        {
            var task = new SemVer
            {
                Major = Major,
                Minor = Minor,
                Patch = Patch,
                Prerelease = Prerelease,
                ShortDate = ShortDate,
                Builds = Builds,
                ShortSHA = sha
            };

            task.BuildEngine = new IO.Tests.MockEngine();

            Assert.True(task.Execute());
            Assert.Equal(ExpectedOutput, task.Version);
        }

        [Theory]
        [MemberData(nameof(GetTestFailCases))]
        public void ExpectToFailTests(Int16 Major, Int16 Minor, Int16 Patch, string Prerelease, Int16 ShortDate, Int16 Builds, string sha)
        {
            var task = new SemVer
            {
                Major = Major,
                Minor = Minor,
                Patch = Patch,
                Prerelease = Prerelease,
                ShortDate = ShortDate,
                Builds = Builds,
                ShortSHA = sha
            };

            task.BuildEngine = new IO.Tests.MockEngine();

            Assert.ThrowsAny<Exception>(() => task.Execute());
        }
    }
}
