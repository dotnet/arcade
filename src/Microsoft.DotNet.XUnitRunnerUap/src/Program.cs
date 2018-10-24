// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Xunit;

namespace Microsoft.DotNet.XUnitRunnerUap
{
    class Program
    {
        volatile static bool cancel = false;

        static int Main(string[] args)
        {
            // Handle RemoteExecutor
            if (args.Length > 0 && args[0] == "remote")
            {
                return RemoteExecutor.Execute(args.Skip(1).ToArray());
            }

            if (args.Length == 0 || args[0] == "-?" || args[0] == "/?" || args[0] == "-h" || args[0] == "--help")
            {
                PrintHeader();
                PrintUsage();
                return 2;
            }

            var commandLine = CommandLine.Parse(args);

            Console.CancelKeyPress += (sender, e) =>
            {
                if (!cancel)
                {
                    Console.WriteLine("Canceling... (Press Ctrl+C again to terminate)");
                    cancel = true;
                    e.Cancel = true;
                }
            };

            if (commandLine.Debug)
            {
                Debugger.Launch();
            }

            if (!commandLine.NoLogo)
                PrintHeader();

            var completionMessages = new ConcurrentDictionary<string, ExecutionSummary>();
            var assembliesElement = new XElement("assemblies");

            int errorCount = 0;
            int failCount = 0;

            foreach (var assembly in commandLine.Project.Assemblies)
            {
                if (cancel)
                {
                    break;
                }

                assembly.Configuration.PreEnumerateTheories = false;
                assembly.Configuration.DiagnosticMessages |= commandLine.DiagnosticMessages;
                assembly.Configuration.AppDomain = AppDomainSupport.Denied;
                var discoveryOptions = TestFrameworkOptions.ForDiscovery(assembly.Configuration);
                var executionOptions = TestFrameworkOptions.ForExecution(assembly.Configuration);
                executionOptions.SetDisableParallelization(true);

                try
                {
                    using (var xunit = new XunitFrontController(AppDomainSupport.Denied, assembly.AssemblyFilename, assembly.ConfigFilename, assembly.Configuration.ShadowCopyOrDefault))
                    using (var discoveryVisitor = new TestDiscoveryVisitor())
                    {
                        string assemblyName = Path.GetFileNameWithoutExtension(assembly.AssemblyFilename);
                        // Discover & filter the tests
                        Console.WriteLine($"Discovering: {assemblyName}");
                        xunit.Find(false, discoveryVisitor, discoveryOptions);
                        discoveryVisitor.Finished.WaitOne();

                        var testCasesDiscovered = discoveryVisitor.TestCases.Count;
                        var filteredTestCases = discoveryVisitor.TestCases.Where(commandLine.Project.Filters.Filter).ToList();
                        var testCasesToRun = filteredTestCases.Count;

                        Console.WriteLine($"Discovered:  {assemblyName}");

                        // Run the filtered tests
                        if (testCasesToRun == 0)
                        {
                            Console.WriteLine($"Info:        {assemblyName} has no tests to run");
                        }
                        else
                        {
                            if (commandLine.Serialize)
                            {
                                filteredTestCases = filteredTestCases.Select(xunit.Serialize).Select(xunit.Deserialize).ToList();
                            }

                            var assemblyElement = new XElement("assembly");

                            StandardUapVisitor resultsVisitor = new StandardUapVisitor(assemblyElement, () => cancel, completionMessages, commandLine.ShowProgress, commandLine.FailSkips);

                            xunit.RunTests(filteredTestCases, resultsVisitor, executionOptions);

                            resultsVisitor.Finished.WaitOne();

                            assembliesElement.Add(assemblyElement);

                            // Set counters to determine the error code later.
                            errorCount += resultsVisitor.ExecutionSummary.Errors;
                            failCount += resultsVisitor.ExecutionSummary.Failed;

                            Console.WriteLine($"{Path.GetFileNameWithoutExtension(assembly.AssemblyFilename)}  Total: {resultsVisitor.ExecutionSummary.Total}, Errors: {resultsVisitor.ExecutionSummary.Errors}, Failed: {resultsVisitor.ExecutionSummary.Failed}, Skipped: {resultsVisitor.ExecutionSummary.Skipped}, Time: {resultsVisitor.ExecutionSummary.Time}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    assembliesElement = new XElement("error");
                    assembliesElement.Add(ex);

                    if (!commandLine.NoColor)
                        Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine($"error: {ex.Message}");

                    if (commandLine.DiagnosticMessages)
                    {
                        if (!commandLine.NoColor)
                            Console.ForegroundColor = ConsoleColor.DarkGray;

                        Console.WriteLine(ex.StackTrace);
                    }
                }
                finally
                {
                    if (!commandLine.NoColor)
                        Console.ResetColor();

                    WriteResults(Path.GetFileName(assembly.AssemblyFilename), assembliesElement).GetAwaiter().GetResult();
                }
            }

            if (cancel)
                return -1073741510;    // 0xC000013A: The application terminated as a result of a CTRL+C

            if (commandLine.Wait)
            {
                Console.WriteLine();
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                Console.WriteLine();
            }

            if (errorCount > 0 || failCount > 0)
                return 1;

            return 0;
        }

        static async Task WriteResults(string test, XElement data)
        {
            StorageFolder folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync("TestResults", CreationCollisionOption.OpenIfExists);
            StorageFile file = await folder.CreateFileAsync(test + ".xml", CreationCollisionOption.ReplaceExisting);

            using (var stream = await file.OpenStreamForWriteAsync())
            {
                data.Save(stream);
                stream.Flush();
            }
        }

        private static void PrintHeader()
        {
            var platform = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var versionAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            Console.WriteLine($"xUnit.net Console Runner v1.0.24 ({IntPtr.Size * 8}-bit {platform})");
        }

        private static void PrintUsage()
        {
            var executableName = "XUnitRunnerUap";

            Console.WriteLine("Copyright (C) .NET Foundation.");
            Console.WriteLine();
            Console.WriteLine($"usage: {executableName} <assemblyFile> [options]");
            Console.WriteLine();
            Console.WriteLine("Valid options:");
            Console.WriteLine("  -nologo                : do not show the copyright message");
            Console.WriteLine("  -nocolor               : do not output results with colors");
            Console.WriteLine("  -failskips             : convert skipped tests into failures");
            Console.WriteLine("  -parallel option       : set parallelization based on option");
            Console.WriteLine("                         :   none        - turn off all parallelization");
            Console.WriteLine("                         :   collections - only parallelize collections");
            Console.WriteLine("                         :   assemblies  - only parallelize assemblies");
            Console.WriteLine("                         :   all         - parallelize assemblies & collections");
            Console.WriteLine("  -maxthreads count      : maximum thread count for collection parallelization");
            Console.WriteLine("                         :   default   - run with default (1 thread per CPU thread)");
            Console.WriteLine("                         :   unlimited - run with unbounded thread count");
            Console.WriteLine("                         :   (number)  - limit task thread pool size to 'count'");
            Console.WriteLine("  -wait                  : wait for input after completion");
            Console.WriteLine("  -diagnostics           : enable diagnostics messages for all test assemblies");
            Console.WriteLine("  -debug                 : launch the debugger to debug the tests");
            Console.WriteLine("  -serialize             : serialize all test cases (for diagnostic purposes only)");
            Console.WriteLine("  -trait \"name=value\"    : only run tests with matching name/value traits");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -notrait \"name=value\"  : do not run tests with matching name/value traits");
            Console.WriteLine("                         : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -method \"name\"         : run a given test method (can be fully specified or use a wildcard;");
            Console.WriteLine("                         : i.e., 'MyNamespace.MyClass.MyTestMethod' or '*.MyTestMethod')");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -nomethod \"name\"       : do not run a given test method (can be fully specified or use a wildcard;");
            Console.WriteLine("                         : i.e., 'MyNamespace.MyClass.MyTestMethod' or '*.MyTestMethod')");
            Console.WriteLine("                         : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -class \"name\"          : run all methods in a given test class (should be fully");
            Console.WriteLine("                         : specified; i.e., 'MyNamespace.MyClass')");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -xml \"name\"          : The xml test results file name");
            Console.WriteLine();
        }
    }
}
