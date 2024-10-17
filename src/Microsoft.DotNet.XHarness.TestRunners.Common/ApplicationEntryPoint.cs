// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// States the type of runner to be used by the application.
/// </summary>
public enum TestRunnerType
{
    NUnit,
    Xunit,
}

/// <summary>
/// Abstract class that represents the entry point of the test application.
///
/// Subclasses must provide the minimum implementation to ensure that:
///
/// Device: We do have the required device information for the logger.
/// Assemblies: Provide a list of the assembly information to run.
///     assemblies can be loaded from disk or from memory, this is up to the
///     implementor.
///
/// Clients that register to the class events and want to update the UI
/// are responsible to do so in the main UI thread. The application entry
/// point does not guarantee that the tests are executed in the ui thread.
///
/// </summary>
public abstract class ApplicationEntryPoint
{

    /// <summary>
    /// Event raised when the test run has started.
    /// </summary>
    public event EventHandler? TestsStarted;

    /// <summary>
    /// Event raised when the test run has completed.
    /// </summary>
    public event EventHandler<TestRunResult>? TestsCompleted;

    // fwd the events from the runner so that clients can connect to them

    /// <summary>
    /// Event raised when a test has started.
    /// </summary>
    public event EventHandler<string>? TestStarted;

    /// <summary>
    /// Event raised when a test has completed or has been skipped.
    /// </summary>
    public event EventHandler<(string TestName, TestResult TestResult)>? TestCompleted;

    protected abstract int? MaxParallelThreads { get; }

    /// <summary>
    /// Must be implemented and return a class that returns the information
    /// of a device. It can return null.
    /// </summary>
    protected abstract IDevice? Device { get; }

    /// <summary>
    /// Returns the IEnumerable with the asseblies that contain the tests
    /// to be ran.
    /// </summary>
    /// <returns></returns>
    protected abstract IEnumerable<TestAssemblyInfo> GetTestAssemblies();

    /// <summary>
    /// Returns the runner to be used.
    /// </summary>
    protected abstract TestRunner GetTestRunner(LogWriter logWriter);

    protected abstract bool IsXunit { get; }

    /// <summary>
    /// Returns the directory that contains the ignore files. In order to ignore certain traits in the
    /// runner the directory most contain one of the two following files:
    ///
    /// * xunit-excludes.txt: Contains the traits to be ignored in xunit.
    /// * nunit-excludes.txt: Contains the categories to be ignored in nunit.
    ///
    /// The default implementation returns null and therefore no traits/categories are ignored.
    ///
    /// If the directory contains any *.ignore files, those will be parse to ignore specific tests that
    /// are known to fail. The format of the file is as follows:
    ///
    /// * A test name per line
    /// * lines that start with # will be ignored and can be used as comments.
    /// * the 'KLASS:' prefix can be used to ignore all the tests in a class.
    /// * the 'Platform32:' prefix can be used to ignore a test but only in a 32b arch device.
    /// </summary>
    protected virtual string? IgnoreFilesDirectory => null;

    /// <summary>
    /// Returns the path to a file that contains the list of traits to ignore in the following format:
    /// traitname=traitvalue
    ///
    /// The default implementation will return null and therefore no traits will be ignored.
    /// </summary>
    protected virtual string? IgnoredTraitsFilePath => null;

    /// <summary>
    /// States if the skipped tests should be logged. Helpful to determine why some tests are executed and others
    /// are not.
    /// </summary>
    protected virtual bool LogExcludedTests { get; } = false;

    /// <summary>
    /// Terminates the application. This should ensure that it is executed
    /// in the main thread.
    /// </summary>
    protected abstract void TerminateWithSuccess();

    /// <summary>
    /// Execute the tests in an async mode.
    /// </summary>
    public abstract Task RunAsync();

    /// <summary>
    /// Get/Set the minimun log level to be used by the runner logging.
    /// </summary>
    public MinimumLogLevel MinimumLogLevel { get; set; } = MinimumLogLevel.Info;

    private void OnTestStarted(object? sender, string testName) => TestStarted?.Invoke(sender, testName);

    private void OnTestCompleted(object? sender, (string TestName, TestResult Testresult) result) => TestCompleted?.Invoke(sender, result);

    private async Task<List<string>> GetIgnoredCategories()
    {
        var categories = new List<string>(); // default known category to ignore

        // check if the child does have an ignore files dir
        if (!string.IsNullOrEmpty(IgnoreFilesDirectory))
        {
            categories.AddRange(await IgnoreFileParser.ParseTraitsContentFileAsync(IgnoreFilesDirectory, IsXunit));
        }

        // check if the child provides a specific traits file.
        if (!string.IsNullOrEmpty((IgnoredTraitsFilePath)))
        {
            categories.AddRange(await IgnoreFileParser.ParseTraitsFileAsync(IgnoredTraitsFilePath));
        }

        return categories;
    }

    internal static void ConfigureRunnerFilters(TestRunner runner, ApplicationOptions options)
    {
        runner.RunAllTestsByDefault = options.RunAllTestsByDefault;

        // Add the provided method and class filters
        if (options.SingleMethodFilters.Count != 0 || options.ClassMethodFilters.Count != 0)
        {
            // Having methods/classes explicitly specified means only those methods/classes should be run
            runner.RunAllTestsByDefault = false;
            foreach (string methodName in options.SingleMethodFilters)
            {
                runner.SkipMethod(methodName, isExcluded: false);
            }

            foreach (string className in options.ClassMethodFilters)
            {
                runner.SkipClass(className, isExcluded: false);
            }
        }
    }

    private static void WriteResults(TestRunner runner, ApplicationOptions options, LogWriter logger, TextWriter writer)
    {
        if (options.EnableXml && writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        if (options.EnableXml)
        {
            runner.WriteResultsToFile(writer, options.XmlVersion);
            logger.Info("Xml file was written to the provided writer.");
        }
        else
        {
            string resultsFilePath = runner.WriteResultsToFile(options.XmlVersion);
            logger.Info($"XML results can be found in '{resultsFilePath}'");
        }
    }

    private async Task<TestRunner> InternalRunAsync(LogWriter logger)
    {
        logger.MinimumLogLevel = MinimumLogLevel;
        var runner = GetTestRunner(logger);
        runner.LogExcludedTests = LogExcludedTests;
        // connect to the runner events so that we fwd them to the client
        runner.TestStarted += OnTestStarted;
        runner.TestCompleted += OnTestCompleted;

        // add ignored categories and specific files
        runner.SkipCategories(await GetIgnoredCategories());
        runner.SkipTests(await IgnoreFileParser.ParseContentFilesAsync(IgnoreFilesDirectory));

        var testAssemblies = GetTestAssemblies();
        // notify the clients we are starting
        TestsStarted?.Invoke(this, new EventArgs());

        await runner.Run(testAssemblies).ConfigureAwait(false);

        var result = new TestRunResult(runner);
        // notify the client we are done and the results, but do not expose
        // the runner.
        TestsCompleted?.Invoke(this, result);
        return runner;
    }

    protected async Task<TestRunner> InternalRunAsync(ApplicationOptions options, TextWriter? loggerWriter, TextWriter? resultsFile)
    {
        // we generate the logs in two different ways depending if the generate xml flag was
        // provided. If it was, we will write the xml file to the provided writer if present, else
        // we will write the normal console output using the LogWriter
        var logger = (loggerWriter == null || options.EnableXml) ? new LogWriter(Device) : new LogWriter(Device, loggerWriter);
        logger.MinimumLogLevel = MinimumLogLevel.Info;
        var runner = await InternalRunAsync(logger);

        WriteResults(runner, options, logger, resultsFile ?? Console.Out);

        logger.Info($"{Environment.NewLine}=== TEST EXECUTION SUMMARY ==={Environment.NewLine}Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests} Skipped: {runner.SkippedTests}{Environment.NewLine}");

        if (options.AppEndTag != null)
        {
            logger.Info(options.AppEndTag);
        }

        if (options.TerminateAfterExecution)
        {
            TerminateWithSuccess();
        }

        return runner;
    }
}
