// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Tests.xUnit;

public class XUnitFiltersCollectionTests
{

    public class FiltersTestData
    {
        public static IEnumerable<object[]> TestCaseFilters
        {
            get
            {
                var testDisplayName = "MyNameSpace.MyClassTest.TestThatFooEqualsBat";

                // no filters, should include
                var collection = new XUnitFiltersCollection { };
                var testCase = new Mock<ITestCase>();
                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        false,
                };

                // single filter that excludes
                // match and exclude
                var filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: true);
                collection = new XUnitFiltersCollection { filter };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        true,
                };

                // single filter that includes the test case in the run
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: false);
                collection = new XUnitFiltersCollection { filter };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        false,
                };

                // one excluding filter, no match, should include
                collection = new XUnitFiltersCollection { };
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: $"not_{testDisplayName}",
                    exclude: true);
                collection = new XUnitFiltersCollection { filter };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);
                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        false,
                };

                // one including filter, no match, should exclude
                collection = new XUnitFiltersCollection { };
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: $"not_{testDisplayName}",
                    exclude: false);
                collection = new XUnitFiltersCollection { filter };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);
                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        true,
                };

                // two excluding filters, match both, should exclude
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: true);
                var filter2 = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: true);
                collection = new XUnitFiltersCollection { filter, filter2 };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        true,
                };

                // two including filters, match both, should include
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: false);
                filter2 = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: false);
                collection = new XUnitFiltersCollection { filter, filter2 };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        false,
                };

                // one filter that includes, other that excludes, match both, should include
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: true);
                filter2 = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: false);
                collection = new XUnitFiltersCollection { filter, filter2 };
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        collection,
                        testCase.Object,
                        false,
                };

                // one filter that includes, other that excludes
                {
                    // match including, should include
                    var excludedTestDisplayName = $"excluded_{testDisplayName}";
                    filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: false);
                    filter2 = XUnitFilter.CreateSingleFilter(
                        singleTestName: excludedTestDisplayName,
                        exclude: true);
                    collection = new XUnitFiltersCollection { filter, filter2 };

                    // match including, should include
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);
                    yield return new object[]
                    {
                            collection,
                            testCase.Object,
                            false,
                    };

                    // match excluding, should exclude
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(excludedTestDisplayName);
                    yield return new object[]
                    {
                            collection,
                            testCase.Object,
                            true,
                    };
                }

                // name filter that excludes, trait filter that includes
                {
                    var traitName = "testTrait";
                    filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: true);
                    filter2 = XUnitFilter.CreateTraitFilter(
                        traitName: traitName,
                        traitValue: null,
                        exclude: false);
                    collection = new XUnitFiltersCollection { filter, filter2 };

                    var matchingTestTraits = new Dictionary<string, List<string>>() { { traitName, new List<string>() } };
                    var notTestDisplayName = $"not_{testDisplayName}";

                    // name match, trait match, should exclude
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);
                    testCase.Setup(t => t.Traits).Returns(matchingTestTraits);
                    yield return new object[]
                    {
                            collection,
                            testCase.Object,
                            true,
                    };

                    // name match, no trait match, should exclude
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);
                    yield return new object[]
                    {
                            collection,
                            testCase.Object,
                            true,
                    };

                    // no name match, trait match, should include
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(notTestDisplayName);
                    testCase.Setup(t => t.Traits).Returns(matchingTestTraits);
                    yield return new object[]
                    {
                            collection,
                            testCase.Object,
                            false,
                    };

                    // no name match, no trait match, should exclude
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(notTestDisplayName);
                    yield return new object[]
                    {
                            collection,
                            testCase.Object,
                            true,
                    };
                }
            }
        }

        public static IEnumerable<object[]> AssemblyFilters
        {
            get
            {
                // single filter, exclude
                var currentAssembly = Assembly.GetExecutingAssembly();
                var assemblyName = $"{currentAssembly.GetName().Name}.dll";
                var assemblyPath = currentAssembly.Location;
                var assemblyInfo = new TestAssemblyInfo(currentAssembly, assemblyPath);
                var filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                var collection = new XUnitFiltersCollection { filter };

                yield return new object[]
                {
                        collection,
                        assemblyInfo,
                        true,
                };

                // single filter, include
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                collection = new XUnitFiltersCollection { filter };

                yield return new object[]
                {
                        collection,
                        assemblyInfo,
                        false,
                };

                // two excluding filters
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                var filter2 = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                collection = new XUnitFiltersCollection { filter, filter2 };

                yield return new object[]
                {
                        collection,
                        assemblyInfo,
                        true,
                };

                // two including filters
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                filter2 = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                collection = new XUnitFiltersCollection { filter, filter2 };

                yield return new object[]
                {
                        collection,
                        assemblyInfo,
                        false,
                };

                // one filter includes, other excludes
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                filter2 = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                collection = new XUnitFiltersCollection { filter, filter2 };

                yield return new object[]
                {
                        collection,
                        assemblyInfo,
                        false,
                };
            }
        }

        [Theory]
        [MemberData(nameof(TestCaseFilters), MemberType = typeof(FiltersTestData))]
        internal void IsExcludedTestCase(XUnitFiltersCollection collection, ITestCase testCase, bool excluded)
        {
            var wasExcluded = collection.IsExcluded(testCase);
            Assert.Equal(excluded, wasExcluded);
        }

        [Theory]
        [MemberData(nameof(AssemblyFilters), MemberType = typeof(FiltersTestData))]
        internal void IsExcludedAsAssembly(XUnitFiltersCollection collection, TestAssemblyInfo assemblyInfo, bool excluded)
        {
            var wasExcluded = collection.IsExcluded(assemblyInfo);
            Assert.Equal(excluded, wasExcluded);
        }
    }

    [Fact]
    public void AssemblyFilters()
    {
        var collection = new XUnitFiltersCollection();

        var assemblies = new[] { "MyFirstAssembly.dll", "SecondAssembly.dll", "ThirdAssembly.exe", };
        collection.AddRange(assemblies.Select(a => XUnitFilter.CreateAssemblyFilter(a, true)));

        var classes = new[] { "FirstClass", "SecondClass", "ThirdClass" };
        collection.AddRange(classes.Select(c => XUnitFilter.CreateClassFilter(c, true)));

        var methods = new[] { "FirstMethod", "SecondMethod" };
        collection.AddRange(methods.Select(m => XUnitFilter.CreateSingleFilter(m, true)));

        var namespaces = new[] { "Namespace" };
        collection.AddRange(namespaces.Select(n => XUnitFilter.CreateNamespaceFilter(n, true)));

        Assert.Equal(assemblies.Length, collection.AssemblyFilters.Count());
    }

    [Fact]
    public void TestCaseFilters()
    {
        var collection = new XUnitFiltersCollection();
        var assemblies = new[] { "MyFirstAssembly.dll", "SecondAssembly.dll", "ThirdAssembly.exe", };
        collection.AddRange(assemblies.Select(a => XUnitFilter.CreateAssemblyFilter(a, true)));

        var classes = new[] { "FirstClass", "SecondClass", "ThirdClass" };
        collection.AddRange(classes.Select(c => XUnitFilter.CreateClassFilter(c, true)));

        var methods = new[] { "FirstMethod", "SecondMethod" };
        collection.AddRange(methods.Select(m => XUnitFilter.CreateSingleFilter(m, true)));

        var namespaces = new[] { "Namespace" };
        collection.AddRange(namespaces.Select(n => XUnitFilter.CreateNamespaceFilter(n, true)));

        Assert.Equal(collection.Count - assemblies.Length, collection.TestCaseFilters.Count());
    }
}
