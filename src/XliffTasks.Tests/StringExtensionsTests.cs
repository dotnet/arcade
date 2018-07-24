// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace XliffTasks.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void GetReplacementCount_NoPlaceholders()
        {
            var text = "Alpha";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 0, actual: replacementCount);
        }

        [Fact]
        public void GetReplacementCount_OneSimplePlaceholder()
        {
            var text = "{0}";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: replacementCount);
        }

        [Fact]
        public void GetReplacementCount_PlaceholderWithAlignment()
        {
            var text = "{0,-3}";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: replacementCount);
        }

        [Fact]
        public void GetReplacementCount_PlaceholderWithFormatString()
        {
            var text = "{0:N}";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: replacementCount);
        }

        [Fact]
        public void GetReplacementCount_WithAlignmentAndFormatString()
        {
            var text = "{0,-10:G1}";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: replacementCount);
        }

        [Fact]
        public void GetReplacementCount_MultiplePlaceholders()
        {
            var text = "{0} {1}";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 2, actual: replacementCount);
        }

        [Fact]
        public void GetReplacementCount_MultipleIdenticalPlaceholders()
        {
            var text = "{2} {2}";
            var replacementCount = text.GetReplacementCount();

            Assert.Equal(expected: 3, actual: replacementCount);
        }
    }
}