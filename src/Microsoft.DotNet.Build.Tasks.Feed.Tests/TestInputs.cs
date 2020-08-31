// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
