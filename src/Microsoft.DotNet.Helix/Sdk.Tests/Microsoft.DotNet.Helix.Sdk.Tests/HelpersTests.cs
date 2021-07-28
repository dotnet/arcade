using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using System.Net;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class HelpersTests
    {
        [Fact]
        public void VerifyEncodedChars()
        {
            var workItemNameWithEncodedChars = "work%20item%20name";

            var actual = Helpers.CleanWorkItemName(workItemNameWithEncodedChars);

            Assert.Equal(workItemNameWithEncodedChars, actual);
        }

        [Fact]
        public void VerifyNotEncodedChars()
        {
            var workItemName = "work item name";

            var actual = Helpers.CleanWorkItemName(workItemName);

            Assert.Equal(WebUtility.UrlDecode(workItemName), actual);
        }

        [Fact]
        public void VerifyNonEncodedFowardSlashIsConverted()
        {
            var workItemNameWithFowardSlash = "work/item/name";
            var workItemNameExpected = "work-item-name";

            var actual = Helpers.CleanWorkItemName(workItemNameWithFowardSlash);

            Assert.Equal(workItemNameExpected, actual);
        }

        [Fact]
        public void FailOnceThenPass()
        {
            string target = Path.Combine(Environment.GetEnvironmentVariable("HELIX_WORKITEM_ROOT") ?? Environment.GetEnvironmentVariable("TEMP"), "my-test-file-123456.snt");
            bool exists = File.Exists(target);
            if (!exists)
            {
                File.WriteAllText(target, "Test failed once");
            }
            
            Assert.True(exists, $"File should exist: {target}");
        }
        
        [MemberData(nameof(TwoThousand))]
        [Theory]
        public void LotsOfTest(int x)
        {
            Assert.Equal(x, x);        
        }

        public static IEnumerable<object[]> TwoThousand()
        {
            for (int i = 0; i < 2000; i++)
                yield return new object[] {i};
        }
    }
}
