// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class NUnitV2TestReportGenerator : TestReportGenerator
{
    public override void GenerateTestReport(TextWriter writer, XmlReader reader)
    {
        if (!reader.ReadToFollowing("test-results"))
        {
            return;
        }

        long.TryParse(reader["errors"], out var errors);
        long.TryParse(reader["failures"], out var failures);
        if (errors == 0 && failures == 0)
        {
            return;
        }

        writer.WriteLine("<div style='padding-left: 15px;'>");
        writer.WriteLine("<ul>");

        void write_failure()
        {
            var name = reader["name"];
            string? message = null;
            var depth = reader.Depth;
            if (reader.ReadToDescendant("message"))
            {
                message = reader.ReadElementContentAsString();
                // ReadToParent
                while (depth > reader.Depth && reader.Read())
                {
                }
            }
            var message_block = message?.IndexOf('\n') >= 0;
            writer.WriteLine("<li>");
            writer.Write(name.AsHtml());
            if (!string.IsNullOrEmpty(message))
            {
                writer.Write(": ");
                if (message_block)
                {
                    writer.WriteLine("<div style='padding-left: 15px;'>");
                }

                writer.Write(message.AsHtml());
                if (message_block)
                {
                    writer.WriteLine("</div>");
                }
            }
            writer.WriteLine("</li>");
        }

        while (reader.ReadToFollowing("test-suite"))
        {
            if (reader["type"] == "Assembly")
            {
                continue;
            }

            var result = reader["result"];
            if (result != "Error" && result != "Failure")
            {
                continue;
            }

            if (result == "Error")
            {
                write_failure();
            }

            var depth = reader.Depth;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Name != "test-case")
                {
                    continue;
                }

                result = reader["result"];
                if (result == "Error" || result == "Failure")
                {
                    write_failure();
                }

                if (reader.Depth < depth)
                {
                    break;
                }
            }
        }

        writer.WriteLine("</ul>");
        writer.WriteLine("</div>");
    }

    public override void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr)
    {
        writer.WriteStartElement("test-results");
        WriteAttributes(writer,
            ("name", title),
            ("total", "1"),
            ("errors", "0"),
            ("failures", "1"),
            ("not-run", "0"),
            ("inconclusive", "0"),
            ("ignored", "0"),
            ("skipped", "0"),
            ("invalid", "0"),
            ("date", XmlConvert.ToString(DateTime.Now, "yyyy-MM-dd")));

        // we are not writting the env and the cunture info since the VSTS uploader does not care
        writer.WriteStartElement("test-suite");
        writer.WriteAttributeString("type", "Assembly");
        WriteNUnitV2TestSuiteAttributes(writer, title);
        writer.WriteStartElement("results");
        writer.WriteStartElement("test-suite");
        writer.WriteAttributeString("type", "TestFixture");
        WriteNUnitV2TestSuiteAttributes(writer, title);
        writer.WriteStartElement("results");
        WriteNUnitV2TestCase(writer, title, message, stderr);
        writer.WriteEndElement(); // results
        writer.WriteEndElement(); // test-suite TextFixture
        writer.WriteEndElement(); // results
        writer.WriteEndElement(); // test-suite Assembly
        writer.WriteEndElement(); // test-results
    }

    private static void WriteNUnitV2TestSuiteAttributes(XmlWriter writer, string title) => WriteAttributes(writer,
        ("name", title),
        ("executed", "True"),
        ("result", "Failure"),
        ("success", "False"),
        ("time", "0"),
        ("asserts", "1"));

    private static void WriteNUnitV2TestCase(XmlWriter writer, string title, string message, TextReader stderr)
    {
        writer.WriteStartElement("test-case");
        WriteAttributes(writer,
            ("name", title),
            ("executed", "True"),
            ("result", "Failure"),
            ("success", "False"),
            ("time", "0"),
            ("asserts", "1")
        );
        writer.WriteStartElement("failure");
        writer.WriteStartElement("message");
        writer.WriteCDataSafe(message);
        writer.WriteEndElement(); // message
        writer.WriteStartElement("stack-trace");
        writer.WriteCDataSafe(stderr.ReadToEnd());
        writer.WriteEndElement(); // stack-trace
        writer.WriteEndElement(); // failure
        writer.WriteEndElement(); // test-case
    }
}
