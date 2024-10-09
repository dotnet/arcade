// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class TrxTestReportGenerator : TestReportGenerator
{
    public override void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr)
    {
        // No-op - there was no implementation when we were splitting the parser up
    }

    public override void GenerateTestReport(TextWriter writer, XmlReader reader)
    {
        var tests = TrxResultParser.ParseTrxXml(reader);
        var failedTests = tests.Where(v => v.Outcome != "Passed" && v.Outcome != "NotExecuted");

        if (failedTests.Any())
        {
            writer.WriteLine("<div style='padding-left: 15px;'>");
            writer.WriteLine("<ul>");
            foreach (var test in failedTests)
            {
                writer.WriteLine("<li>");
                writer.Write($"{test.ClassName?.AsHtml()}.{test.TestName?.AsHtml()}");
                if (!string.IsNullOrEmpty(test.Message))
                {
                    writer.Write(": ");
                    writer.Write(test.Message?.AsHtml());
                }
                writer.WriteLine("<br />");
                writer.WriteLine("</li>");
            }
            writer.WriteLine("</ul>");
            writer.WriteLine("</div>");
        }
    }
}
