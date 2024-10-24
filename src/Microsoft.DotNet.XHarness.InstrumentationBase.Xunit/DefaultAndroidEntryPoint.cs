// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;

namespace Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit;

/// <summary>
/// Class that implements basic functionality for the entry point of
/// the Android test application.
///
/// The minimum required to run succesfully the test app based on
/// DefaultAndroidEntryPoint is to provide results-file-path as an argument.
///
/// Client is able to provide test assemblies by overriding
/// GetTestAssemblies method.
///
/// Other methods such as Device, Logger etc. have default implementation
/// but can be overrided if needed.
///
/// </summary>
public class DefaultAndroidEntryPoint : AndroidApplicationEntryPoint
{
    private readonly string _resultsPath;
    private readonly string? _excludeCategoriesDir;
    private readonly string? _excludeCategoriesFile;
    private readonly Dictionary<string, string> _parsedArguments;

    public const string ResultsFileArgumentName = "results-file-name";
    public const string ResultsFileArgumentPath = "results-file-path";
    public const string ExcludeCategoriesDirArgumentName = "exclude-categories-dir";
    public const string ExcludeCategoriesFileArgumentName = "exclude-categories-file";
    public const string ExcludeMethodArgumentName = "exclude-method";
    public const string IncludeMethodArgumentName = "include-method";
    public const string ExcludeClassArgumentName = "exclude-class";
    public const string IncludeClassArgumentName = "include-class";

    protected override string? IgnoreFilesDirectory => _excludeCategoriesDir;
    protected override string? IgnoredTraitsFilePath => _excludeCategoriesFile;

    public DefaultAndroidEntryPoint(string resultsPath, Dictionary<string, string> optionalBundle)
    {
        _parsedArguments = optionalBundle;

        // use default name for test results file
        _parsedArguments.TryAdd(ResultsFileArgumentName, "TestResults.xml");

        if (!Directory.Exists(resultsPath))
        {
            Directory.CreateDirectory(resultsPath);
        }

        _resultsPath = Path.Combine(resultsPath, _parsedArguments[ResultsFileArgumentName]);
        _excludeCategoriesDir = _parsedArguments.GetValueOrDefault(ExcludeCategoriesDirArgumentName);
        _excludeCategoriesFile = _parsedArguments.GetValueOrDefault(ExcludeCategoriesFileArgumentName);
    }

    protected override bool LogExcludedTests => true;

    public override TextWriter? Logger => null;

    public override string TestsResultsFinalPath => _resultsPath;

    protected override int? MaxParallelThreads => System.Environment.ProcessorCount / 2;

    protected override IDevice? Device => null;

    public IEnumerable<Assembly> Tests { get; set; } = new List<Assembly>();
    protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
    {
        foreach (var assembly in Tests)
        {
            yield return new TestAssemblyInfo(assembly, assembly.Location);
        }
    }
    protected override void TerminateWithSuccess()
    {
    }

    private static void ConfigureFilters(string? filter, Action<string, bool> filterMethod, bool isExcluded)
    {
        if (filter != null)
        {
            foreach (var f in filter.Split(' '))
            {
                filterMethod(f, isExcluded);
            }
        }
    }

    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
        var testRunner = base.GetTestRunner(logWriter);

        (string? Filter, Action<string, bool> FilterMethod, bool IsExcluded)[] filters =
        {
                (_parsedArguments.GetValueOrDefault(ExcludeMethodArgumentName), testRunner.SkipMethod, true),
                (_parsedArguments.GetValueOrDefault(ExcludeClassArgumentName), testRunner.SkipClass, true),
                (_parsedArguments.GetValueOrDefault(IncludeMethodArgumentName), testRunner.SkipMethod, false),
                (_parsedArguments.GetValueOrDefault(IncludeClassArgumentName), testRunner.SkipClass, false)
            };

        foreach (var t in filters)
        {
            ConfigureFilters(t.Filter, t.FilterMethod, t.IsExcluded);
        }

        return testRunner;
    }
}
