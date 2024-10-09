// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;
using Moq;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Tests.xUnit;

public class XUnitFilterTests
{

    public class FiltersTestData
    {
        public static IEnumerable<object[]> TraitFilters
        {
            get
            {
                const string traitName = "MyTrait";
                const string traitValue = "MyValue";
                // no traits, should not be excluded
                var filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: null,
                    exclude: true);
                var testCase = new Mock<ITestCase>();
                var method = new Mock<ITestMethod>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>());
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);
                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // test is not exclude since we are exclude and have no traits
                        "",
                };
                // no traits, included
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: null,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>());
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);
                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // no traits and filter is included, means we exclude
                        "[FILTER] Excluded test",
                };
                // trait present, no value, exclude
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: null,
                    exclude: true);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>
                {
                    [traitName] = null!,
                });
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);
                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // have the trait, no values and no values in the filter
                        "[FILTER] Excluded test",
                };
                // trait present, no value, include
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: null,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>
                {
                    [traitName] = null!,
                });
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // have the trait and we are including
                        "[FILTER] Included test",
                };
                // trait present, preset value, exclude
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: traitValue,
                    exclude: true);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>
                {
                    [traitName] = new List<string> { traitValue },
                });
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // exclude, got the trait and value
                        "[FILTER] Excluded test",
                };
                // trait present, present value, include
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: traitValue,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>
                {
                    [traitName] = new List<string> { traitValue },
                });
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);
                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // we are including
                        "[FILTER] Included test",
                };
                // trait present, missing value, exclude
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: traitValue,
                    exclude: true);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>
                {
                    [traitName] = new List<string> { new string('$', 4) },
                });
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);
                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // not excluded, we have the trait, not the value
                        "[FILTER] Included test",
                };
                // trait present, missing value, include
                filter = XUnitFilter.CreateTraitFilter(
                    traitName: traitName,
                    traitValue: traitValue,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.Traits).Returns(new Dictionary<string, List<string>>
                {
                    [traitName] = new List<string> { new string('$', 4) },
                });
                testCase.Setup(t => t.TestMethod).Returns(method.Object);
                method.Setup(m => m.Method).Returns((IMethodInfo)null!);
                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // we are including, but do not have the correct value
                        "[FILTER] Excluded test",
                };
            }
        }

        public static IEnumerable<object[]> TypeNameFilters
        {
            get
            {
                var testClass = "MyClass";
                // null/empty class, means the opposite, is like a no match
                var filter = XUnitFilter.CreateClassFilter(className: testClass, exclude: true);
                var testMethod = new Mock<ITestMethod>();
                var testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod).Returns(testMethod.Object);
                testMethod.Setup(t => t.TestClass).Returns((ITestClass)null!);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false,
                        "",
                };
                // not null test name no match, excluded
                filter = XUnitFilter.CreateClassFilter(className: testClass, exclude: true);
                testMethod = new Mock<ITestMethod>();
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod).Returns(testMethod.Object);
                testMethod.Setup(t => t.TestClass.Class.Name).Returns("OtherClass");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // test has to be included
                        "",
                };

                // not null name match, exclude
                filter = XUnitFilter.CreateClassFilter(className: testClass, exclude: true);
                testMethod = new Mock<ITestMethod>();
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod).Returns(testMethod.Object);
                testMethod.Setup(t => t.TestClass.Class.Name).Returns(testClass);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // exclude
                        "[FILTER] Excluded test",
                };

                // not null name match, include
                filter = XUnitFilter.CreateClassFilter(className: testClass, exclude: false);
                testMethod = new Mock<ITestMethod>();
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod).Returns(testMethod.Object);
                testMethod.Setup(t => t.TestClass.Class.Name).Returns(testClass);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // include
                        "[FILTER] Included test",
                };
            }
        }

        public static IEnumerable<object[]> SingleFilters
        {
            get
            {
                var testDisplayName = "MyNameSpace.MyClassTest.TestThatFooEqualsBat";
                // match and exclude
                var filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: true);
                var testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // we do exclude
                        "[FILTER] Excluded test",
                };

                // match an include
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // we do include
                        "[FILTER] Included test",
                };

                // not match, exclude, therefore include
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: true);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns("OtherTest");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // we do include
                        "[FILTER] Included test",
                };

                // not match, include, therefore exclude
                filter = XUnitFilter.CreateSingleFilter(
                    singleTestName: testDisplayName,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns("OtherTest");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // we do exclude
                        "[FILTER] Excluded test",
                };
            }
        }

        public static IEnumerable<object[]> NamespaceFilters
        {
            get
            {
                var testNamespace = "MyNameSpace";
                // null and exclude, therefore include
                var filter = XUnitFilter.CreateNamespaceFilter(
                    namespaceName: testNamespace,
                    exclude: true);
                var testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod.TestClass.Class.Name).Returns((string)null!);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // we do include
                        "[FILTER] Included test",
                };

                // null and include, therefore exclude
                filter = XUnitFilter.CreateNamespaceFilter(
                    namespaceName: testNamespace,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod.TestClass.Class.Name).Returns((string)null!);

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // we do include
                        "[FILTER] Excluded test",
                };

                // match and exclude
                filter = XUnitFilter.CreateNamespaceFilter(
                    namespaceName: testNamespace,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.DisplayName).Returns(testNamespace);
                testCase.Setup(t => t.TestMethod.TestClass.Class.Name).Returns($"{testCase}.MyClass");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // we do exclude
                        "[FILTER] Excluded test",
                };

                // match and include
                filter = XUnitFilter.CreateNamespaceFilter(
                    namespaceName: testNamespace,
                    exclude: true);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod.TestClass.Class.Name).Returns($"{testCase}.MyClass");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // we do include
                        "[FILTER] Included test",
                };

                // no match and exclude, therefore include
                filter = XUnitFilter.CreateNamespaceFilter(
                    namespaceName: testNamespace,
                    exclude: true);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod.TestClass.Class.Name).Returns("OtherNamespace.MyClass");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        false, // we do include
                        "[FILTER] Included test",
                };

                // no match and include, therefore exclude
                filter = XUnitFilter.CreateNamespaceFilter(
                    namespaceName: testNamespace,
                    exclude: false);
                testCase = new Mock<ITestCase>();
                testCase.Setup(t => t.TestMethod.TestClass.Class.Name).Returns("OtherNamespace.MyClass");

                yield return new object[]
                {
                        filter,
                        testCase.Object,
                        true, // we do exclude
                        "[FILTER] Excluded test",
                };
            }
        }

        public static IEnumerable<object[]> AssemblyFilters
        {
            get
            {
                var currentAssembly = Assembly.GetExecutingAssembly();
                var assemblyName = $"{currentAssembly.GetName().Name}.dll";
                var assemblyPath = currentAssembly.Location;
                var assemblyInfo = new TestAssemblyInfo(currentAssembly, assemblyPath);
                // assembly name match, exclude
                var filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                yield return new object[]
                {
                        filter,
                        assemblyInfo,
                        true, // exclude
                        "[FILTER] Excluded assembly",
                };

                // assembly name match, include
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName, exclude: false);
                yield return new object[]
                {
                        filter,
                        assemblyInfo,
                        false, // include
                        "[FILTER] Included assembly",
                };

                // assembly name no match, exclude
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: "OtherAssembly.dll", exclude: true);
                yield return new object[]
                {
                        filter,
                        assemblyInfo,
                        false, // include
                        "[FILTER] Included assembly",
                };

                // assembly name no match, include
                filter = XUnitFilter.CreateAssemblyFilter(assemblyName: "OtherAssembly.dll", exclude: false);
                yield return new object[]
                {
                        filter,
                        assemblyInfo,
                        true, // exclude
                        "[FILTER] Excluded assembly",
                };
            }
        }

        [Theory]
        [MemberData(nameof(TraitFilters), MemberType = typeof(FiltersTestData))]
        [MemberData(nameof(TypeNameFilters), MemberType = typeof(FiltersTestData))]
        [MemberData(nameof(SingleFilters), MemberType = typeof(FiltersTestData))]
        [MemberData(nameof(NamespaceFilters), MemberType = typeof(FiltersTestData))]
        internal void ApplyFilters(XUnitFilter filter, ITestCase testCase, bool excluded, string logMessage)
        {
            var logOutut = new StringBuilder();
            Action<string>? log = (s) =>
            {
                logOutut.AppendLine(s);
            };
            var testExcluded = filter.IsExcluded(testCase, log);
            Assert.Equal(excluded, testExcluded);
            // validate with the log
            Assert.StartsWith(logMessage, logOutut.ToString().Trim());
        }

        [Theory]
        [MemberData(nameof(AssemblyFilters), MemberType = typeof(FiltersTestData))]
        internal void ApplyAssemblyFilter(XUnitFilter filter, TestAssemblyInfo info, bool excluded, string logMessage)
        {
            var logOutut = new StringBuilder();
            Action<string>? log = (s) =>
            {
                logOutut.AppendLine(s);
            };
            var testExcluded = filter.IsExcluded(info, log);
            Assert.Equal(excluded, testExcluded);
            // validate with the log
            Assert.StartsWith(logMessage, logOutut.ToString().Trim());
        }
    }

    [Fact]
    public void CreateSingleFilterNullTestName()
    {
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateSingleFilter(null!, true));
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateSingleFilter("", true));
    }

    [Theory]
    [InlineData("TestMethod", "TestAssembly", true)]
    [InlineData("TestMethod", "TestAssembly", false)]
    [InlineData("TestMethod", null, false)]
    public void CreateSingleFilter(string methodName, string? assemblyName, bool excluded)
    {
        var filter = XUnitFilter.CreateSingleFilter(methodName, excluded, assemblyName);
        Assert.Equal(methodName, filter.SelectorValue);
        Assert.Equal(assemblyName, filter.AssemblyName);
        Assert.Equal(excluded, filter.Exclude);
        Assert.Equal(XUnitFilterType.Single, filter.FilterType);
    }

    [Fact]
    public void CreateAssemblyFilterNullAssemblyName()
    {
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateAssemblyFilter(null!, true));
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateAssemblyFilter("", true));
    }

    [Theory]
    [InlineData("MyTestAssembly.exe", true)]
    [InlineData("MySecondAssembly.dll", true)]
    [InlineData("MyTestAssembly.dll", false)]
    public void CreateAssemblyFilter(string assemblyName, bool excluded)
    {
        var filter = XUnitFilter.CreateAssemblyFilter(assemblyName, excluded);
        Assert.Null(filter.SelectorName);
        Assert.Equal(assemblyName, filter.AssemblyName);
        Assert.Equal(excluded, filter.Exclude);
        Assert.Equal(XUnitFilterType.Assembly, filter.FilterType);
    }

    [Fact]
    public void CreateAssemblyFilterMissingExtension() => Assert.Throws<ArgumentException>(() => XUnitFilter.CreateAssemblyFilter("MissinExtension", true));

    [Fact]
    public void CreateNamespaceFilterNullNameSpace()
    {
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateNamespaceFilter(null!, true));
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateNamespaceFilter("", true));
    }

    [Theory]
    [InlineData("MyNameSpace", "MyAssembly", true)]
    [InlineData("MyNameSpace", "MyAssembly", false)]
    [InlineData("MyNameSpace", null, false)]
    public void CreateNamespaceFilter(string nameSpace, string? assemblyName, bool excluded)
    {
        var filter = XUnitFilter.CreateNamespaceFilter(nameSpace, excluded, assemblyName);
        Assert.Equal(nameSpace, filter.SelectorValue);
        Assert.Equal(assemblyName, filter.AssemblyName);
        Assert.Equal(excluded, filter.Exclude);
        Assert.Equal(XUnitFilterType.Namespace, filter.FilterType);
    }

    [Fact]
    public void CreateClassFilterNullClassName()
    {
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateClassFilter(null!, true));
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateClassFilter("", true));
    }

    [Theory]
    [InlineData("MyClass", "MyAssembly", true)]
    [InlineData("MyClass", "MyAssembly", false)]
    [InlineData("MyClass", null, false)]
    public void CreateClassFilter(string className, string? assemblyName, bool excluded)
    {
        var filter = XUnitFilter.CreateClassFilter(className, excluded, assemblyName);
        Assert.Equal(className, filter.SelectorValue);
        Assert.Equal(assemblyName, filter.AssemblyName);
        Assert.Equal(excluded, filter.Exclude);
        Assert.Equal(XUnitFilterType.TypeName, filter.FilterType);
    }

    [Fact]
    public void CreateTraitFilterNullTrait()
    {
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateTraitFilter(null!, "value", true));
        Assert.Throws<ArgumentException>(() => XUnitFilter.CreateTraitFilter("", "value", true));
    }

    [Theory]
    [InlineData("MyTrait", "MyTraitValue", true)]
    [InlineData("MyTrait", "MyTraitValue", false)]
    [InlineData("MyTrait", null, false)]
    public void CreateTraitFilter(string trait, string? traitValue, bool excluded)
    {
        var filter = XUnitFilter.CreateTraitFilter(trait, traitValue, excluded);
        Assert.Equal(trait, filter.SelectorName);
        if (traitValue == null)
        {
            Assert.Equal(string.Empty, filter.SelectorValue);
        }
        else
        {
            Assert.Equal(traitValue, filter.SelectorValue);
        }
        Assert.Null(filter.AssemblyName);
        Assert.Equal(excluded, filter.Exclude);
        Assert.Equal(XUnitFilterType.Trait, filter.FilterType);
    }

    [Theory]
    [InlineData(XUnitFilterType.Namespace)]
    [InlineData(XUnitFilterType.Single)]
    [InlineData(XUnitFilterType.Trait)]
    [InlineData(XUnitFilterType.TypeName)]
    internal void ApplyWrongTypeToAssembly(XUnitFilterType type)
    {
        // build and assembly for the given type
        XUnitFilter? filter = null;
        switch (type)
        {
            case XUnitFilterType.Namespace:
                filter = XUnitFilter.CreateNamespaceFilter("foo", true);
                break;
            case XUnitFilterType.Single:
                filter = XUnitFilter.CreateSingleFilter("foo", true);
                break;
            case XUnitFilterType.Trait:
                filter = XUnitFilter.CreateTraitFilter("foo", null, true);
                break;
            case XUnitFilterType.TypeName:
                filter = XUnitFilter.CreateClassFilter("foo", true);
                break;
            default:
                Assert.Fail("Unexpected filter type");
                break;
        }
        var assebly = new TestAssemblyInfo(Assembly.GetAssembly(typeof(XUnitFilterType)), "path");
        Assert.Throws<InvalidOperationException>(() => filter?.IsExcluded(assebly));
    }
}
