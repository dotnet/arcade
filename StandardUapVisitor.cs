using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace Xunit.ConsoleClient
{
    public class StandardUapVisitor : XmlTestExecutionVisitor
    {
        private string _assemblyName;
        private readonly ConcurrentDictionary<string, ExecutionSummary> _completionMessages;
        private readonly StreamWriter _log;
        private readonly bool _showProgress;
        private readonly bool _failSkips;

        public StandardUapVisitor(XElement assemblyElement,
                                     Func<bool> cancelThunk,
                                     StreamWriter log,
                                     ConcurrentDictionary<string, ExecutionSummary> completionMessages,
                                     bool showProgress,
                                     bool failSkips)
            : base(assemblyElement, cancelThunk)
        {
            _completionMessages = completionMessages;
            _log = log;
            _showProgress = showProgress;
            _failSkips = failSkips;
        }

        public ExecutionSummary ExecutionSummary 
        {
            get 
            {
                ExecutionSummary summary;
                if (_completionMessages.TryGetValue(_assemblyName, out summary))
                {
                    return summary;
                }

                return new ExecutionSummary();
            }
        }

        protected override bool Visit(ITestAssemblyStarting assemblyStarting)
        {
            _assemblyName = Path.GetFileNameWithoutExtension(assemblyStarting.TestAssembly.Assembly.AssemblyPath);

            _log.WriteLine($"Starting:    {_assemblyName}");

            return base.Visit(assemblyStarting);
        }

        protected override bool Visit(ITestAssemblyFinished assemblyFinished)
        {
            // Base class does computation of results, so call it first.
            var result = base.Visit(assemblyFinished);

            _log.WriteLine($"Finished:    {_assemblyName}");

            _completionMessages.TryAdd(_assemblyName, new ExecutionSummary
            {
                Total = assemblyFinished.TestsRun,
                Failed = !_failSkips ? assemblyFinished.TestsFailed : assemblyFinished.TestsFailed + assemblyFinished.TestsSkipped,
                Skipped = !_failSkips ? assemblyFinished.TestsSkipped : 0,
                Time = assemblyFinished.ExecutionTime,
                Errors = Errors
            });

            return result;
        }

        protected override bool Visit(ITestFailed testFailed)
        {
            _log.WriteLine($"   {XmlEscape(testFailed.Test.DisplayName)} [FAIL]");
            _log.WriteLine($"      {ExceptionUtility.CombineMessages(testFailed).Replace(Environment.NewLine, Environment.NewLine + "      ")}");

            WriteStackTrace(ExceptionUtility.CombineStackTraces(testFailed));

            return base.Visit(testFailed);
        }

        protected override bool Visit(ITestPassed testPassed)
        {
            return base.Visit(testPassed);
        }

        protected override bool Visit(ITestSkipped testSkipped)
        {
            if (_failSkips)
            {
                return Visit(new TestFailed(testSkipped.Test, 0M, "", new[] { "FAIL_SKIP" }, new[] { testSkipped.Reason }, new[] { "" }, new[] { -1 }));
            }

            _log.WriteLine($"   {XmlEscape(testSkipped.Test.DisplayName)} [SKIP]");
            _log.WriteLine($"      {XmlEscape(testSkipped.Reason)}");

            return base.Visit(testSkipped);
        }

        protected override bool Visit(ITestStarting testStarting)
        {
            if (_showProgress)
            {
                _log.WriteLine($"   {XmlEscape(testStarting.Test.DisplayName)} [STARTING]");
            }
            return base.Visit(testStarting);
        }

        protected override bool Visit(ITestFinished testFinished)
        {
            if (_showProgress)
            {
                _log.WriteLine($"   {XmlEscape(testFinished.Test.DisplayName)} [FINISHED] Time: {testFinished.ExecutionTime}s");
            }
            return base.Visit(testFinished);
        }

        protected override bool Visit(IErrorMessage error)
        {
            WriteError("FATAL", error);

            return base.Visit(error);
        }

        protected override bool Visit(ITestAssemblyCleanupFailure cleanupFailure)
        {
            WriteError($"Test Assembly Cleanup Failure ({cleanupFailure.TestAssembly.Assembly.AssemblyPath})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCaseCleanupFailure cleanupFailure)
        {
            WriteError($"Test Case Cleanup Failure ({cleanupFailure.TestCase.DisplayName})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestClassCleanupFailure cleanupFailure)
        {
            WriteError($"Test Class Cleanup Failure ({cleanupFailure.TestClass.Class.Name})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCollectionCleanupFailure cleanupFailure)
        {
            WriteError($"Test Collection Cleanup Failure ({cleanupFailure.TestCollection.DisplayName})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCleanupFailure cleanupFailure)
        {
            WriteError($"Test Cleanup Failure ({cleanupFailure.Test.DisplayName})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestMethodCleanupFailure cleanupFailure)
        {
            WriteError($"Test Method Cleanup Failure ({cleanupFailure.TestMethod.Method.Name})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected void WriteError(string failureName, IFailureInformation failureInfo)
        {
            _log.WriteLine($"   [{failureName}] {XmlEscape(failureInfo.ExceptionTypes[0])}");
            _log.WriteLine($"      {XmlEscape(ExceptionUtility.CombineMessages(failureInfo))}");

            WriteStackTrace(ExceptionUtility.CombineStackTraces(failureInfo));
        }

        void WriteStackTrace(string stackTrace)
        {
            if (String.IsNullOrWhiteSpace(stackTrace))
                return;

            _log.WriteLine("      Stack Trace:");

            foreach (var stackFrame in stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                _log.WriteLine($"         {StackFrameTransformer.TransformFrame(stackFrame, Directory.GetCurrentDirectory())}");
            }
        }
    }
}