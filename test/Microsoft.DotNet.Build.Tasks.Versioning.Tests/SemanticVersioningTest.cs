// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Versioning.Tests
{
    public class SemanticVersioningTest
    {
        public static IEnumerable<object[]> GetTestSuccessCases()
        {
            yield return new object[] { 1, 2, 0, "preview1", "08530",  0, "asdf34234", "dev", "1.2.0-preview1.08530.0+asdf34234" };
            yield return new object[] { 3, 0, 1,    "beta2", "26405", 10,  "asd34523", "dev", "3.0.1-beta2.26405.10+asd34523" };
            yield return new object[] { 1, 2, 3, "preview1", "45678", 48, "asdf34234", "dev", "1.2.3-preview1.45678.48+asdf34234" };

            yield return new object[] { 1, 2, 0, "preview1", "08530", 0, "asdf34234", "stable-prerelease", "1.2.0-preview1" };

            yield return new object[] { 1, 2, 3, "preview1", "45678", 48, "asdf34234", "final", "1.2.3" };

            yield return new object[] { 1, 0, 0, "", "", 0, "", "{Major}", "1" };
            yield return new object[] { 0, 1, 0, "", "", 0, "", "{Minor}", "1" };
            yield return new object[] { 0, 0, 1, "", "", 0, "", "{Patch}", "1" };
            yield return new object[] { 0, 0, 0, "rc/1", "", 0, "", "{Prerelease}", "rc/1" };
            yield return new object[] { 0, 0, 0, "", "12345", 0, "", "{ShortDate}", "12345" };
            yield return new object[] { 0, 0, 0, "", "", 1, "", "{Builds}", "1" };
            yield return new object[] { 0, 0, 0, "", "", 0, "11111111", "{ShortSha}", "11111111" };

            yield return new object[] { 12, 90, 78, "56", "34", 2, "1", "{ShortSha}{Builds}{ShortDate}{Prerelease}{Patch}{Minor}{Major}", "123456789012" };
        }

        public static IEnumerable<object[]> GetTestFailCases()
        {
            // Shouldn't accept 0 Major version
            yield return new object[] { 0, 0, 0, String.Empty, "0", 0, String.Empty, "dev"};
            yield return new object[] { 0, 1, 1, "Microsoft", "1", 1, ".NET", "dev" };

            yield return new object[] { 1, 2, 3, String.Empty, "1", 1, "Arcade", "dev" };

            // No format field
            yield return new object[] { 1, 2, 3, String.Empty, "1", 1, "Arcade", "Major" };
            yield return new object[] { 1, 2, 3, String.Empty, "1", 1, "Arcade", "" };

            // Invalid formatting
            yield return new object[] { 1, 2, 3, String.Empty, "1", 1, "Arcade", "{Major}{Minor{Patch}" };
            yield return new object[] { 1, 2, 3, String.Empty, "1", 1, "Arcade", "{Major}Minor}{Patch}" };
            yield return new object[] { 1, 2, 3, String.Empty, "1", 1, "Arcade", "{Major.{Minor}}" };
        }

        [Theory]
        [MemberData(nameof(GetTestSuccessCases))]
        public void ExpectToPassTests(UInt16 Major, UInt16 Minor, UInt16 Patch, string Prerelease, string ShortDate, UInt16 Builds, string sha, string Format, string ExpectedOutput)
        {
            var task = new Versioning
            {
                Major = Major,
                Minor = Minor,
                Patch = Patch,
                Prerelease = Prerelease,
                ShortDate = ShortDate,
                Builds = Builds,
                ShortSHA = sha,
                FormatString = Format
            };

            task.BuildEngine = new TestsUtil.MockEngine();

            Assert.True(task.Execute());
            Assert.Equal(ExpectedOutput, task.VersionString);
        }

        [Theory]
        [MemberData(nameof(GetTestFailCases))]
        public void ExpectToFailTests(UInt16 Major, UInt16 Minor, UInt16 Patch, string Prerelease, string ShortDate, UInt16 Builds, string sha, string Format)
        {
            var task = new Versioning
            {
                Major = Major,
                Minor = Minor,
                Patch = Patch,
                Prerelease = Prerelease,
                ShortDate = ShortDate,
                Builds = Builds,
                ShortSHA = sha,
                FormatString = Format
            };

            task.BuildEngine = new TestsUtil.MockEngine();

            Assert.ThrowsAny<Exception>(() => task.Execute());
        }
    }
}
