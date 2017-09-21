using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.UI.Xaml;
using Xunit;
using Xunit.ConsoleClient;
using Xunit.Shared;

namespace XUnit.Runner.Uap
{
    class XunitTestRunner
    {
        volatile static bool cancel = false;

        private static StreamWriter log;

        public async void RunTests(string arguments)
        {
            log = await Helpers.GetFileStreamWriterInLocalStorageAsync("stdout.txt");

            string[] args = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            log.WriteLine(arguments);

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
                        string assemblyName = Path.GetFileNameWithoutExtension(assembly.AssemblyFilename);
                        // Discover & filter the tests
                        log.WriteLine($"Discovering: {assemblyName}");
                        xunit.Find(false, discoveryVisitor, discoveryOptions);
                        discoveryVisitor.Finished.WaitOne();

                        var testCasesDiscovered = discoveryVisitor.TestCases.Count;
                        var filteredTestCases = discoveryVisitor.TestCases.Where(commandLine.Project.Filters.Filter).ToList();
                        var testCasesToRun = filteredTestCases.Count;

                        log.WriteLine($"Discovered: {assemblyName}");

                        // Run the filtered tests
                        if (testCasesToRun == 0)
                        {
                            log.WriteLine($"Info:        {assemblyName} has no tests to run");
                        }
                        else
                        {
                            if (commandLine.Serialize)
                            {
                                filteredTestCases = filteredTestCases.Select(xunit.Serialize).Select(xunit.Deserialize).ToList();
                            }

                            var assemblyElement = new XElement("assembly");

                            StandardUapVisitor resultsVisitor = new StandardUapVisitor(assemblyElement, () => cancel, log, completionMessages, commandLine.ShowProgress, commandLine.FailSkips);

                            xunit.RunTests(filteredTestCases, resultsVisitor, executionOptions);

                            resultsVisitor.Finished.WaitOne();

                            assembliesElement.Add(assemblyElement);

                            log.WriteLine($"{Path.GetFileNameWithoutExtension(assembly.AssemblyFilename)}  Total: {resultsVisitor.ExecutionSummary.Total}, Errors: {resultsVisitor.ExecutionSummary.Errors}, Failed: {resultsVisitor.ExecutionSummary.Failed}, Skipped: {resultsVisitor.ExecutionSummary.Skipped}, Time: {resultsVisitor.ExecutionSummary.Time}");
                        }
                    }
                }
                catch (Exception e)
                {
                    assembliesElement = new XElement("error");
                    assembliesElement.Add(e);
                    log.WriteLine(e);
                }
                finally
                {
                    await WriteResults(Path.GetFileName(assembly.AssemblyFilename), assembliesElement);
                    log.Dispose();
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
    }
}
