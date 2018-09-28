// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using VersionManager.Tests.Models;
using Xunit;
using Manager = Microsoft.DotNet.Maestro.Tasks;

namespace VersionManager.Tests
{
    public class VersionTests
    {
        [Fact]
        public void ValidateVersions()
        {
            List<TestAsset> testAssets = GetTestAssets();

            foreach (TestAsset testAsset in testAssets)
            {
                string version = Manager.VersionManager.GetVersion(testAsset.Name);
                Assert.Equal(version, testAsset.ExpectedVersion);
            }
        }

        private List<TestAsset> GetTestAssets()
        {
            List<TestAsset> testAssets = new List<TestAsset>();
            string[] assets = File.ReadAllLines("Assets.csv");

            foreach (string input in assets)
            {
                if (!string.IsNullOrEmpty(input))
                {
                    string[] values = input.Split(',');
                    string name = values[0];
                    string expectedVersion = string.IsNullOrEmpty(values[1]) ? null : values[1];
                    testAssets.Add(new TestAsset(name, expectedVersion));
                }
            }

            return testAssets;
        }
    }
}
