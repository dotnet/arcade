// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ApiCompat.Tests
{
    public class AttributeDifferenceTests
    {
        private readonly string _implementationPath = Path.Combine(AppContext.BaseDirectory, "Implementation", "AttributeDifference.dll");
        private readonly string _contractPath = Path.Combine(AppContext.BaseDirectory, "Contract");

        [Fact]
        public void AttributeDifferenceIsFound()
        {

            string runOutput = Helpers.RunApiCompat(_implementationPath, _contractPath, "implementation", "contract");

            Assert.Contains("CannotRemoveAttribute : Attribute 'System.ComponentModel.DesignerAttribute' exists on 'AttributeDifference.AttributeDifferenceClass1' in the implementation but not the contract.", runOutput);
            Assert.Contains("CannotRemoveAttribute : Attribute 'AttributeDifference.FooAttribute' exists on 'AttributeDifference.AttributeDifferenceClass1' in the implementation but not the contract.", runOutput);
            Assert.Contains("Total Issues: 2", runOutput);
        }

        [Fact]
        public void AttributeDifferenceIsFoundWithExcludeAttributesFile()
        {
            using TempFile excludeAttributesFile = TempFile.Create();

            File.WriteAllLines(excludeAttributesFile.Path, new string[] { "T:System.ComponentModel.DisplayNameAttribute", "T:AttributeDifference.FooAttribute" });

            string runOutput = Helpers.RunApiCompat(_implementationPath, new string[] { _contractPath }, new string[] { excludeAttributesFile.Path }, "implementation", "contract");
            Assert.Contains("CannotRemoveAttribute : Attribute 'System.ComponentModel.DesignerAttribute' exists on 'AttributeDifference.AttributeDifferenceClass1' in the implementation but not the contract.", runOutput);
            Assert.Contains("Total Issues: 1", runOutput);
        }

        [Fact]
        public void NoIssuesWithExcludeAttributesFile()
        {
            using TempFile excludeAttributesFile = TempFile.Create();

            File.WriteAllLines(excludeAttributesFile.Path, new string[] { "T:System.ComponentModel.DesignerAttribute", "T:AttributeDifference.FooAttribute" });

            string runOutput = Helpers.RunApiCompat(_implementationPath, new string[] { _contractPath }, new string[] { excludeAttributesFile.Path }, null, null);

            Assert.Contains("Total Issues: 0", runOutput);
        }

    }
}
