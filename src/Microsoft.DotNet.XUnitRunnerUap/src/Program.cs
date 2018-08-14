// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            var commandLine = CommandLine.Parse(args);
            if (commandLine.Debug)
            {
                Debugger.Launch();
            }

            var completionMessages = new ConcurrentDictionary<string, ExecutionSummary>();
            var assembliesElement = new XElement("assemblies");

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

                            Console.WriteLine($"{Path.GetFileNameWithoutExtension(assembly.AssemblyFilename)}  Total: {resultsVisitor.ExecutionSummary.Total}, Errors: {resultsVisitor.ExecutionSummary.Errors}, Failed: {resultsVisitor.ExecutionSummary.Failed}, Skipped: {resultsVisitor.ExecutionSummary.Skipped}, Time: {resultsVisitor.ExecutionSummary.Time}");
                        }
                    }
                }
                catch (Exception e)
                {
                    assembliesElement = new XElement("error");
                    assembliesElement.Add(e);
                    Console.WriteLine(e);
                }
                finally
                {
                    WriteResults(Path.GetFileName(assembly.AssemblyFilename), assembliesElement).GetAwaiter().GetResult();
                }
            }

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
    }
}
