// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using NUnit.Engine;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

internal class NUnitTestRunner : TestRunner, INUnitTestRunner
{
    private readonly FilterBuilder _testFilterBuilder;
    private readonly NUnitTestListener _testListener;
    private ResultSummary? _results;
    private bool _runAssemblyByDefault;

    public NUnitTestRunner(LogWriter logger) : base(logger)
    {
        _testListener = new NUnitTestListener(this, logger);
        _testFilterBuilder = new FilterBuilder(new TestFilterBuilder());
    }

    private Dictionary<string, bool>? AssemblyFilters { get; set; }

    protected override string ResultsFileName { get; set; } = "TestResults.NUnit.xml";

    public bool GCAfterEachFixture { get; set; }

    public void IncreasePassedTests()
    {
        PassedTests++;
        ExecutedTests++;
    }

    public void IncreaseSkippedTests() => SkippedTests++;

    public void IncreaseFailedTests()
    {
        FailedTests++;
        ExecutedTests++;
    }

    public void IncreaseInconclusiveTests()
    {
        InconclusiveTests++;
        ExecutedTests++;
    }

    public void Add(TestFailureInfo info)
        => FailureInfos.Add(info ?? throw new ArgumentNullException(nameof(info)));

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        if (testAssemblies == null)
        {
            throw new ArgumentNullException(nameof(testAssemblies));
        }

        if (AssemblyFilters == null || AssemblyFilters.Count == 0)
        {
            _runAssemblyByDefault = true;
        }
        else
        {
            _runAssemblyByDefault = AssemblyFilters.Values.Any(v => !v);
        }

        ITestEngine engine = TestEngineActivator.CreateInstance();
        TestFilter filter = _testFilterBuilder.GetFilter();
        // use the current executing assembly full name as the name of the test suit that groups all the diff assemblies
        _results = new ResultSummary(Assembly.GetExecutingAssembly().FullName!, this);

        TotalTests = 0;
        foreach (TestAssemblyInfo assemblyInfo in testAssemblies)
        {
            if (assemblyInfo == null || assemblyInfo.Assembly == null || !ShouldRunAssembly(assemblyInfo))
            {
                continue;
            }

            var testPackage = new TestPackage(assemblyInfo.FullPath);
            ITestRunner runner = engine.GetRunner(testPackage);
            TotalTests += runner.CountTestCases(filter);
            ITestRun result;
            try
            {
                OnAssemblyStart(assemblyInfo.Assembly);
                result = await Task.Run(() => runner.RunAsync(_testListener, filter)).ConfigureAwait(false);
            }
            finally
            {
                OnAssemblyFinish(assemblyInfo.Assembly);
            }

            if (result == null)
            {
                continue;
            }

            _results.Add(result);
        }

        FilteredTests = TotalTests - ExecutedTests;
        LogFailureSummary();
    }

    private bool ShouldRunAssembly(TestAssemblyInfo assemblyInfo)
    {
        if (assemblyInfo == null)
        {
            return false;
        }

        if (AssemblyFilters == null || AssemblyFilters.Count == 0)
        {
            return true;
        }

        if (AssemblyFilters.TryGetValue(assemblyInfo.FullPath, out bool include))
        {
            return ReportFilteredAssembly(assemblyInfo, include);
        }

        string fileName = Path.GetFileName(assemblyInfo.FullPath);
        if (AssemblyFilters.TryGetValue(fileName, out include))
        {
            return ReportFilteredAssembly(assemblyInfo, include);
        }

        fileName = Path.GetFileNameWithoutExtension(assemblyInfo.FullPath);
        if (AssemblyFilters.TryGetValue(fileName, out include))
        {
            return ReportFilteredAssembly(assemblyInfo, include);
        }

        return _runAssemblyByDefault;
    }

    private bool ReportFilteredAssembly(TestAssemblyInfo assemblyInfo, bool include)
    {
        if (!LogExcludedTests)
        {
            return include;
        }

        const string included = "Included";
        const string excluded = "Excluded";

        OnInfo($"[FILTER] {(include ? included : excluded)} assembly: {assemblyInfo.FullPath}");
        return include;
    }

    public override string WriteResultsToFile(XmlResultJargon jargon)
    {
        if (_results == null)
        {
            return string.Empty;
        }

        string ret = GetResultsFilePath();
        if (string.IsNullOrEmpty(ret))
        {
            return string.Empty;
        }

        jargon.GetWriter().WriteResultFile(_results, ret);

        return ret;
    }

    public override void WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        if (_results == null)
        {
            return;
        }

        jargon.GetWriter().WriteResultFile(_results, writer);
    }

    public override void SkipTests(IEnumerable<string> tests)
        => _testFilterBuilder.IgnoredMethods.AddRange(tests);

    public override void SkipCategories(IEnumerable<string> categories)
        => _testFilterBuilder.IgnoredCategories.AddRange(categories);

    public override void SkipMethod(string method, bool _)
        => _testFilterBuilder.IgnoredMethods.Add(method);

    public override void SkipClass(string className, bool _)
        => _testFilterBuilder.IgnoredClasses.Add(className);
}
