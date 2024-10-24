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

public class XUnitTestReportGenerator : TestReportGenerator
{
    public override void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr)
    {
        writer.WriteStartElement("assemblies");
        writer.WriteStartElement("assembly");
        WriteAttributes(writer,
            ("name", title),
            ("environment", "64-bit .NET Standard [collection-per-class, non-parallel]"),
            ("test-framework", "xUnit.net 2.4.1.0"),
            ("run-date", XmlConvert.ToString(DateTime.Now, "yyyy-MM-dd")),
            ("total", "1"),
            ("passed", "0"),
            ("failed", "1"),
            ("skipped", "0"),
            ("time", "0"),
            ("errors", "0"));
        writer.WriteStartElement("collection");
        WriteAttributes(writer,
            ("total", "1"),
            ("passed", "0"),
            ("failed", "1"),
            ("skipped", "0"),
            ("name", title),
            ("time", "0"));
        writer.WriteStartElement("test");
        WriteAttributes(writer,
            ("name", title),
            ("type", "TestApp"),
            ("method", "Run"),
            ("time", "0"),
            ("result", "Fail"));
        WriteFailure(writer, message, stderr);
        writer.WriteEndElement(); // test
        writer.WriteEndElement(); // collection
        writer.WriteEndElement(); // assembly
        writer.WriteEndElement(); // assemblies
    }

    public override void GenerateTestReport(TextWriter writer, XmlReader reader)
    {
        var failedTests = new List<(string name, string message)>();
        // xUnit is not as nice and does not provide the final result in a top node,
        // we need to look in all the collections and find all the failed tests, this is really bad :/
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "collection")
            {
                continue;
            }

            reader.ReadToDescendant("test");
            do
            {
                if (reader.Name != "test")
                {
                    break;
                }
                // read the test cases in the current node
                var status = reader["result"];
                switch (status)
                {
                    case "Fail":
                        string name = reader["name"] ?? throw new InvalidOperationException();
                        reader.ReadToDescendant("message");
                        string message = reader.ReadElementContentAsString();
                        failedTests.Add((name, message));
                        break;
                }
            } while (reader.ReadToNextSibling("test"));
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
}
