// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.SignCheck.Verification;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class SignCheckExclusionsTests
    {
        private const string RequiredJavaScriptFile = "required-signed-file.js";

        [Fact]
        public void ExactRequiredSignedFileOverridesWildcardDoNotSign()
        {
            Exclusions exclusions = CreateJavaScriptExclusions(RequiredJavaScriptFile);
            string nestedPath = $"package/content/{RequiredJavaScriptFile}";

            Assert.False(exclusions.IsDoNotSign(nestedPath, null, null, null));
            Assert.False(exclusions.IsExcluded(nestedPath, null, null, null));
        }

        [Fact]
        public void OtherJavaScriptAndSuffixCollisionsRemainDoNotSign()
        {
            Exclusions exclusions = CreateJavaScriptExclusions(RequiredJavaScriptFile);

            Assert.True(exclusions.IsDoNotSign("ordinary.js", null, null, null));
            Assert.True(exclusions.IsDoNotSign($"not-{RequiredJavaScriptFile}", null, null, null));
        }

        [Fact]
        public void RequiredSignedFileMatchingIsCaseInsensitiveAndNestedPathAware()
        {
            Exclusions exclusions = CreateJavaScriptExclusions($"nested/path/{RequiredJavaScriptFile}");

            Assert.False(exclusions.IsDoNotSign(
                "extracted-file.js",
                "package.nupkg",
                "package.nupkg/NESTED/PATH/REQUIRED-SIGNED-FILE.JS",
                @"NESTED\PATH\REQUIRED-SIGNED-FILE.JS"));
        }

        [Fact]
        public void WildcardRequiredSignedFileIsRejected()
        {
            var exclusions = new Exclusions();

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                exclusions.AddRequiredSignedFile("required-*.js"));
            Assert.Contains("must be an exact path or file name; wildcards are not supported", error.Message);
        }

        [Fact]
        public void MustSignCommentDoesNotMakeFileRequiredSigned()
        {
            var exclusions = new Exclusions();
            exclusions.Add(new Exclusion("*.js;; DO-NOT-SIGN"));
            exclusions.Add(new Exclusion($"{RequiredJavaScriptFile};; MUST-SIGN"));

            Assert.True(exclusions.IsDoNotSign(RequiredJavaScriptFile, null, null, null));
        }

        private static Exclusions CreateJavaScriptExclusions(string requiredSignedFile)
        {
            var exclusions = new Exclusions();
            exclusions.Add(new Exclusion("*.js;; DO-NOT-SIGN"));
            exclusions.AddRequiredSignedFile(requiredSignedFile);
            return exclusions;
        }
    }
}
