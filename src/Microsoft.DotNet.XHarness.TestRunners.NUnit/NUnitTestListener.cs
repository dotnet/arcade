// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Xml;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using NUnit;
using NUnit.Engine;
using NUnit.Framework.Interfaces;

namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

internal class NUnitTestListener : ITestEventListener
{
    private readonly LogWriter _logger;
    private readonly INUnitTestRunner _runner;

    public NUnitTestListener(INUnitTestRunner runner, LogWriter logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void TestStarted(XmlNode testEvent)
    {
        if (testEvent == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_runner.TestsRootDirectory))
        {
            Environment.CurrentDirectory = _runner.TestsRootDirectory;
        }

        _logger.OnInfo(testEvent.Attributes["fullname"].Value);

    }

    private void TestFinished(XmlNode testEvent)
    {
        // we need to get the status from the string. That value is stored in
        // <test-case result=''> value can be: Passed, Failed, Inconclusive or Skipped. To make things
        // 'simpler' we also need to check the label, which might have the following values:  Error, Cancelled or Invalid
        var result = testEvent.GetAttribute("label") ?? testEvent.GetAttribute("result");
        TestStatus status = result switch
        {
            "Passed" => TestStatus.Passed,
            "Failed" => TestStatus.Failed,
            "Inconclusive" => TestStatus.Inconclusive,
            "Skipped" => TestStatus.Skipped,
            _ => TestStatus.Inconclusive // Cancelled, error or invalid
        };
        var testName = testEvent.Attributes["fullname"].Value;
        var sb = new StringBuilder();
        switch (status)
        {
            case TestStatus.Passed:
                sb.Append("\t[PASS] ");
                _runner.IncreasePassedTests();
                break;
            case TestStatus.Skipped:
                sb.Append("\t[IGNORED] ");
                _runner.IncreaseSkippedTests();
                break;
            case TestStatus.Failed:
                sb.Append("\t[FAIL] ");
                _runner.IncreaseFailedTests();
                break;
            case TestStatus.Inconclusive:
                sb.Append("\t[INCONCLUSIVE] ");
                _runner.IncreaseInconclusiveTests();
                break;
            default:
                sb.Append("\t[INFO] ");
                break;
        }

        sb.Append(testName);
        // if we skipped or we failed, add some extra info to the logging:
        if (status == TestStatus.Failed)
        {
            var messageNodes = testEvent.SelectNodes("failure/message");
            if (messageNodes.Count == 1)
            {
                if (messageNodes[0].ChildNodes.Count == 1) // should not happen, but I trust no one
                {
                    var cDataNode = messageNodes[0].ChildNodes[0];
                    var message = cDataNode?.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        message = message.Replace("\r\n", "\\r\\n");
                        sb.Append($" : {message}");
                    }
                }
            }
            // get the stack trace, similar to the message node
            var stacktraceNodes = testEvent.SelectNodes("failure/stack-trace");
            if (stacktraceNodes.Count == 1)
            {
                if (stacktraceNodes[0].ChildNodes.Count == 1) // should not happen, but I trust no one
                {
                    var cDataNode = messageNodes[0].ChildNodes[0];
                    var stackTrace = cDataNode?.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(stackTrace))
                    {
                        stackTrace = stackTrace.Replace("\r\n", "\\r\\n");
                        string[] lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            sb.AppendLine($"\t\t{line}");
                        }
                    }
                }
            }

            _runner.Add(new TestFailureInfo
            {
                TestName = testName,
                Message = sb.ToString()
            });
        }

        _logger.OnInfo(sb.ToString());
    }

    public void OnTestEvent(string report)
    {
        // again, not a simple api, the report string is an xml, that contains a fragment of xml
        // which depends on the event type, load the xml, do the appropriate thing.
        var doc = new XmlDocument();
        doc.LoadXml(report);

        var testEvent = doc.FirstChild;
        switch (testEvent.Name)
        {
            case "start-test":
                TestStarted(testEvent);
                break;

            case "test-case":
                TestFinished(testEvent);
                break;
        }
    }
}
