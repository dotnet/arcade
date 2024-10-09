// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class NUnitV3TestReportGenerator : TestReportGenerator
{
    public override void GenerateTestReport(TextWriter writer, XmlReader reader)
    {
        var failedTests = new List<(string name, string message)>();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "test-run")
            {
                long.TryParse(reader["failed"], out var failed);
                if (failed == 0)
                {
                    break;
                }
            }

            if (reader.NodeType == XmlNodeType.Element && reader.Name == "test-suite" && (reader["type"] == "TestFixture" || reader["type"] == "ParameterizedFixture"))
            {
                reader.ReadToDescendant("test-case");
                do
                {
                    if (reader.Name != "test-case")
                    {
                        break;
                    }
                    // read the test cases in the current node
                    var status = reader["result"];
                    switch (status)
                    {
                        case "Error":
                        case "Failed":
                            string name = reader["name"] ?? throw new InvalidOperationException();
                            var subtree = reader.ReadSubtree();
                            subtree.ReadToDescendant("message");
                            string message = subtree.ReadElementContentAsString();
                            while (subtree.Read()) { } // read to end of subtree
                            failedTests.Add((name, message));
                            break;

                    }
                } while (reader.ReadToNextSibling("test-case"));
            }
        }

        if (failedTests.Count > 0)
        {
            writer.WriteLine("<div style='padding-left: 15px;'>");
            writer.WriteLine("<ul>");
            foreach (var (name, message) in failedTests)
            {
                writer.WriteLine("<li>");
                writer.Write(name.AsHtml());
                if (!string.IsNullOrEmpty(message))
                {
                    writer.Write(": ");
                    writer.Write(message.AsHtml());
                }
                writer.WriteLine("<br />");
                writer.WriteLine("</li>");
            }
            writer.WriteLine("</ul>");
            writer.WriteLine("</div>");
        }
    }

    public override void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr)
    {
        var date = DateTime.Now;
        writer.WriteStartElement("test-run");
        // defualt values for the crash
        WriteAttributes(writer,
            ("name", title),
            ("testcasecount", "1"),
            ("result", "Failed"),
            ("time", "0"),
            ("total", "1"),
            ("passed", "0"),
            ("failed", "1"),
            ("inconclusive", "0"),
            ("skipped", "0"),
            ("asserts", "1"),
            ("run-date", XmlConvert.ToString(date, "yyyy-MM-dd")),
            ("start-time", date.ToString("HH:mm:ss"))
        );
        writer.WriteStartElement("test-suite");
        writer.WriteAttributeString("type", "Assembly");
        WriteNUnitV3TestSuiteAttributes(writer, title);
        WriteFailure(writer, "Child test failed");
        writer.WriteStartElement("test-suite");
        WriteAttributes(writer,
            ("name", title),
            ("fullname", title),
            ("type", "TestFixture"),
            ("testcasecount", "1"),
            ("result", "Failed"),
            ("time", "0"),
            ("total", "1"),
            ("passed", "0"),
            ("failed", "1"),
            ("inconclusive", "0"),
            ("skipped", "0"),
            ("asserts", "1"));
        writer.WriteStartElement("test-case");
        WriteAttributes(writer,
            ("id", "1"),
            ("name", title),
            ("fullname", title),
            ("result", "Failed"),
            ("time", "0"),
            ("asserts", "1"));
        WriteFailure(writer, message, stderr);
        writer.WriteEndElement(); // test-case
        writer.WriteEndElement(); // test-suite = TestFixture
        writer.WriteEndElement(); // test-suite = Assembly
        writer.WriteEndElement(); // test-run
    }

    private static void WriteNUnitV3TestSuiteAttributes(XmlWriter writer, string title) => WriteAttributes(writer,
        ("id", "1"),
        ("name", title),
        ("testcasecount", "1"),
        ("result", "Failed"),
        ("time", "0"),
        ("total", "1"),
        ("passed", "0"),
        ("failed", "1"),
        ("inconclusive", "0"),
        ("skipped", "0"),
        ("asserts", "0"));
}
