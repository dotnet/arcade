using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace XliffTasks.Tests
{
    static class AssertHelper
    {
        public static void AssertWithoutLineEndingDifference(string expected, string actual)
        {
            Assert.Equal(
                expected.Replace("\r", string.Empty),
                actual.Replace("\r", string.Empty));
        }
    }
}
