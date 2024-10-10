// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class TouchUnitTestReportGenerator : TestReportGenerator
{
    public override void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr)
    {
        // No-op - there was no implementation when we were splitting the parser up
    }

    public override void GenerateTestReport(TextWriter writer, XmlReader reader)
    {
        while (reader.Read())
        {

            if (reader.NodeType != XmlNodeType.Element || reader.Name != "test-suite" || reader["type"] != "TestFixture" && reader["type"] != "TestCollection")
            {
                continue;
            }

            long.TryParse(reader["errors"], out var errors);
            long.TryParse(reader["failed"], out var failed);

            // if we do not have any errors, return, nothing to be written here
            if (errors == 0 && failed == 0)
            {
                return;
            }

            writer.WriteLine("<div style='padding-left: 15px;'>");
            writer.WriteLine("<ul>");

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
                { // only interested in errors
                    case "Error":
                    case "Failure":
                        writer.WriteLine("<li>");
                        var test_name = reader["name"];
                        writer.Write(test_name.AsHtml());
                        // read to the message of the error and get it
                        reader.ReadToDescendant("message");
                        var message = reader.ReadElementContentAsString();
                        if (!string.IsNullOrEmpty(message))
                        {
                            writer.Write(": ");
                            writer.Write(message.AsHtml());
                        }
                        writer.WriteLine("<br />");
                        writer.WriteLine("</li>");
                        break;
                }
            } while (reader.ReadToNextSibling("test-case"));

            writer.WriteLine("</ul>");
            writer.WriteLine("</div>");
        }
    }
}
