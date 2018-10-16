// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class NugetPropertyStringProviderTests
    {

        [Fact]
        public void ShouldParseSingleValidKeyValue()
        {
            var expectedDictionary = new Dictionary<string, string>{ {"a", "b"} };
            AssertPropertyStringsParseToDictionary(new[] { "a=b" }, expectedDictionary);
            AssertPropertyStringsParseToDictionary(new[] { " a=b " }, expectedDictionary);
        }

        [Fact]
        public void ShouldParseSingleValidKeyValueWithEqualsInValue()
        {
            var expectedDictionary = new Dictionary<string, string> { { "a", "=b=" } };
            AssertPropertyStringsParseToDictionary(new[] { "a==b=" }, expectedDictionary);
        }

        [Fact]
        public void ShouldParseSingleValidKeyValueWithMultineContents()
        {
            var multiLineString = @"b
                                    c";
            var expectedDictionary = new Dictionary<string, string> { { "a", multiLineString } };
            AssertPropertyStringsParseToDictionary(new[] { $"a={multiLineString}" }, expectedDictionary);

            expectedDictionary = new Dictionary<string, string> { { multiLineString, "b" } };
            AssertPropertyStringsParseToDictionary(new[] { $"{multiLineString}=b" }, expectedDictionary);

            expectedDictionary = new Dictionary<string, string> { { multiLineString, multiLineString } };
            AssertPropertyStringsParseToDictionary(new[] { $"{multiLineString}={multiLineString}" }, expectedDictionary);
        }

        [Fact]
        public void ShouldReturnNullOnNullInput()
        {
            Assert.Null(NuspecPropertyStringProvider.GetNuspecPropertyDictionary(null));
        }

        [Fact]
        public void ShouldReturnEmptyDictionaryOnEmptyInput()
        {
            var expectedDictionary = new Dictionary<string, string>();
            AssertPropertyStringsParseToDictionary(new string[0], expectedDictionary);
        }

        [Fact]
        public void ShouldFailWithNoEquals()
        {
            Assert.Throws<InvalidDataException>(() => NuspecPropertyStringProvider.GetNuspecPropertyDictionary(new[] { "abc" }));
        }

        [Fact]
        public void ShouldFailWithNoValue()
        {
            Assert.Throws<InvalidDataException>(() => NuspecPropertyStringProvider.GetNuspecPropertyDictionary(new[] { "a= " }));
        }

        [Fact]
        public void ShouldFailWithNoKey()
        {
            Assert.Throws<InvalidDataException>(() => NuspecPropertyStringProvider.GetNuspecPropertyDictionary(new[] { " = b" }));
        }

        [Fact]
        public void ShouldFailWithNoKeyAndValue()
        {
            Assert.Throws<InvalidDataException>(() => NuspecPropertyStringProvider.GetNuspecPropertyDictionary(new[] { " = " }));
        }

        private static void AssertPropertyStringsParseToDictionary(string[] propertyStrings, Dictionary<string, string> expectedDictionary)
        {
            Assert.Equal(expectedDictionary, NuspecPropertyStringProvider.GetNuspecPropertyDictionary(propertyStrings));
        }
    }
}
