// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace VersionManager.Tests.Models
{
    public class TestAsset
    {
        public TestAsset(string name, string expectedVersion)
        {
            Name = name;
            ExpectedVersion = expectedVersion;
        }

        public string Name { get; set; }

        public string ExpectedVersion { get; set; }
    }
}
