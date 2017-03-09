using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Xunit;
using Xunit.Shared;

namespace XUnit.Runner.Uap
{
    class XunitTestRunner
    {
        volatile static bool cancel = false;

        private static StringBuilder log;

        public async void RunTests(string arguments, StringBuilder sbLog, Action<string> uiLogger)
        {
            log = sbLog;

            var reporters = new List<IRunnerReporter>();

            string[] args = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            log.AppendLine($"Args: {arguments}");

            var commandLine = CommandLine.Parse(args);
            if (commandLine.Debug)
            {
                Debugger.Launch();
            }
            var reporterMessageHandler = commandLine.Reporter.CreateMessageHandler(new RunLogger(uiLogger));
            var completionMessages = new ConcurrentDictionary<string, ExecutionSummary>();
            var assembliesElement = new XElement("assemblies");

            foreach (var assembly in commandLine.Project.Assemblies)
            {
                if (cancel)
                {
                    return;
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
                        // Discover & filter the tests
                        reporterMessageHandler.OnMessage(new TestAssemblyDiscoveryStarting(assembly, false, false, discoveryOptions));
                        xunit.Find(false, discoveryVisitor, discoveryOptions);
                        discoveryVisitor.Finished.WaitOne();

                        var testCasesDiscovered = discoveryVisitor.TestCases.Count;
                        var filteredTestCases = discoveryVisitor.TestCases.Where(commandLine.Project.Filters.Filter).ToList();
                        var testCasesToRun = filteredTestCases.Count;
                        //log.AppendLine("testCasesToRun: " + testCasesToRun);

                        reporterMessageHandler.OnMessage(new TestAssemblyDiscoveryFinished(assembly, discoveryOptions, testCasesDiscovered, testCasesToRun));

                        // Run the filtered tests
                        if (testCasesToRun == 0)
                        {
                            completionMessages.TryAdd(Path.GetFileName(assembly.AssemblyFilename), new ExecutionSummary());
                        }
                        else
                        {
                            if (commandLine.Serialize)
                            {
                                filteredTestCases = filteredTestCases.Select(xunit.Serialize).Select(xunit.Deserialize).ToList();
                            }

                            reporterMessageHandler.OnMessage(new TestAssemblyExecutionStarting(assembly, executionOptions));
                            var assemblyElement = new XElement("assembly");

                            IExecutionVisitor resultsVisitor = new XmlAggregateVisitor(reporterMessageHandler, completionMessages, assemblyElement, () => cancel);
                            if (commandLine.FailSkips)
                            {
                                resultsVisitor = new FailSkipVisitor(resultsVisitor);
                            }

                            xunit.RunTests(filteredTestCases, resultsVisitor, executionOptions);

                            //log.AppendLine("Finished running tests");
                            resultsVisitor.Finished.WaitOne();

                            reporterMessageHandler.OnMessage(new TestAssemblyExecutionFinished(assembly, executionOptions, resultsVisitor.ExecutionSummary));
                            assembliesElement.Add(assemblyElement);

                            log.AppendLine($"{Path.GetFileNameWithoutExtension(assembly.AssemblyFilename)}  Total: {resultsVisitor.ExecutionSummary.Total}, Errors: {resultsVisitor.ExecutionSummary.Errors}, Failed: {resultsVisitor.ExecutionSummary.Failed}, Time: {resultsVisitor.ExecutionSummary.Time}");
                        }
                    }
                }
                catch (Exception e)
                {
                    assembliesElement = new XElement("error");
                    assembliesElement.Add(e);
                }
                finally
                {
                    await WriteResults(Path.GetFileName(assembly.AssemblyFilename), assembliesElement);
                    await WriteLogs(Path.GetFileName(assembly.AssemblyFilename), log.ToString());
                }
            }

            Application.Current.Exit();
        }


        static async Task WriteResults(string test, XElement data)
        {
            var file = await Helpers.GetStorageFileAsync($"{test}.xml");
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                data.Save(stream);
                stream.Flush();
            }
        }

        static async Task WriteLogs(string test, string data)
        {
            var file = await Helpers.GetStorageFileAsync($"{test}.txt");
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    await sw.WriteAsync(data);
                }
            }
        }
    }
}
