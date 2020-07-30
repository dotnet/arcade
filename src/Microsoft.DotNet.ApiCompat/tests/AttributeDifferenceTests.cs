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
        private readonly string _contractPath = Path.Combine(AppContext.BaseDirectory, "Contract", "AttributeDifference.dll");

        [Fact]
        public void AttributeDifferenceIsFound()
        {
            using TempFile implementation = TempFile.CreateFromPath(_implementationPath);
            using TempFile contract = TempFile.CreateFromPath(_contractPath);

            string runOutput = Helpers.RunApiCompat(implementation.Path, Path.GetDirectoryName(contract.Path), "implementation", "contract");

            Assert.Contains("CannotRemoveAttribute : Attribute 'System.ComponentModel.DesignerAttribute' exists on 'AttributeDifference.AttributeDifferenceClass1' in the implementation but not the contract.", runOutput);
            Assert.Contains("Total Issues: 1", runOutput);
        }

        [Fact]
        public void NoIssuesWithExcludeAttributesFile()
        {
            using TempFile implementation = TempFile.CreateFromPath(_implementationPath);
            using TempFile contract = TempFile.CreateFromPath(_contractPath);
            using TempFile excludeAttributesFile = TempFile.Create();

            File.WriteAllText(excludeAttributesFile.Path, "T:System.ComponentModel.DesignerAttribute");

            string runOutput = Helpers.RunApiCompat(implementation.Path, new string[] { Path.GetDirectoryName(contract.Path) }, new string[] { excludeAttributesFile.Path }, null, null);

            Assert.Contains("Total Issues: 0", runOutput);
        }
    }
}
