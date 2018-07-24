// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace XliffTasks.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void GetReplacementCount_NoPlaceHolders()
        {
            var text = "Alpha";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 0, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_OneSimplePlaceHolder()
        {
            var text = "{0}";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_PlaceHolderWithAlignment()
        {
            var text = "{0,-3}";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_PlaceHolderWithFormatString()
        {
            var text = "{0:N}";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_WithAlignmentAndFormatString()
        {
            var text = "{0,-10:G1}";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_MultiplePlaceHolders()
        {
            var text = "{0} {1}";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 2, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_MultipleIdenticalPlaceHolders()
        {
            var text = "{2} {2}";
            var maxPlaceHolderCount = text.GetReplacementCount();

            Assert.Equal(expected: 3, actual: maxPlaceHolderCount);
        }
    }
}