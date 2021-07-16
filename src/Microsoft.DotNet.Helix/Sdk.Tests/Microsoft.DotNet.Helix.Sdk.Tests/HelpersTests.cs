using Xunit;
using System.Net;

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
    }
}
