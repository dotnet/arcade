// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Xml;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class NUnitV3ResultParser : IXmlResultParser
{
    public (string resultLine, bool failed) ParseXml(TextReader source, TextWriter? humanReadableOutput)
    {
        long testcasecount, passed, failed, inconclusive, skipped;
        var failedTestRun = false; // result = "Failed"
        testcasecount = passed = failed = inconclusive = skipped = 0L;

        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

        using (var reader = XmlReader.Create(source, settings))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "test-run")
                {
                    long.TryParse(reader["testcasecount"], out testcasecount);
                    long.TryParse(reader["passed"], out passed);
                    long.TryParse(reader["failed"], out failed);
                    long.TryParse(reader["inconclusive"], out inconclusive);
                    long.TryParse(reader["skipped"], out skipped);
                    failedTestRun = failed != 0;
                }

                if (humanReadableOutput != null && reader.NodeType == XmlNodeType.Element && reader.Name == "test-suite")
                {
                    ParseNUnitV3XmlTestSuite(reader, humanReadableOutput, false);
                }
            }
        }

        var resultLine = $"Tests run: {testcasecount} Passed: {passed} Inconclusive: {inconclusive} Failed: {failed} Ignored: {skipped + inconclusive}";
        humanReadableOutput?.WriteLine(resultLine);
        return (resultLine, failedTestRun);
    }

    private static void ParseNUnitV3XmlTestCase(XmlReader reader, TextWriter humanReadableOutput)
    {
        if (reader.NodeType != XmlNodeType.Element)
        {
            throw new InvalidOperationException();
        }

        if (reader.Name != "test-case")
        {
            throw new InvalidOperationException();
        }

        // read the test cases in the current node
        var status = reader["result"];
        switch (status)
        {
            case "Passed":
                humanReadableOutput.Write("\t[PASS] ");
                break;
            case "Skipped":
                humanReadableOutput.Write("\t[IGNORED] ");
                break;
            case "Error":
            case "Failed":
                humanReadableOutput.Write("\t[FAIL] ");
                break;
            case "Inconclusive":
                humanReadableOutput.Write("\t[INCONCLUSIVE] ");
                break;
            default:
                humanReadableOutput.Write("\t[INFO] ");
                break;
        }
        var name = reader["name"];
        humanReadableOutput.Write(name);
        if (status == "Failed")
        { //  we need to print the message
            reader.ReadToDescendant("failure");
            reader.ReadToDescendant("message");
            humanReadableOutput.Write($" : {reader.ReadElementContentAsString()}");
            if (reader.Name != "stack-trace")
            {
                reader.ReadToNextSibling("stack-trace");
            }

            humanReadableOutput.Write($" : {reader.ReadElementContentAsString()}");
        }
        if (status == "Skipped")
        { // nice to have the skip reason
            reader.ReadToDescendant("reason");
            reader.ReadToDescendant("message");
            humanReadableOutput.Write($" : {reader.ReadElementContentAsString()}");
        }
        // add a new line
        humanReadableOutput.WriteLine();
    }

    private static void ParseNUnitV3XmlTestSuite(XmlReader reader, TextWriter humanReadableOutput, bool nested)
    {
        if (reader.NodeType != XmlNodeType.Element)
        {
            throw new InvalidOperationException();
        }

        if (reader.Name != "test-suite")
        {
            throw new InvalidOperationException();
        }

        var type = reader["type"];
        var isFixture = type == "TestFixture" || type == "ParameterizedFixture";
        var testCaseName = reader["fullname"];
        var time = reader.GetAttribute("time") ?? "0"; // some nodes might not have the time :/

        if (isFixture)
        {
            humanReadableOutput.WriteLine(testCaseName);
        }

        if (reader.IsEmptyElement)
        {
            if (isFixture)
            {
                humanReadableOutput.WriteLine($"{testCaseName} {time} ms");
            }

            return;
        }

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (reader.Name == "test-case")
                    {
                        ParseNUnitV3XmlTestCase(reader, humanReadableOutput);
                    }
                    else if (reader.Name == "test-suite")
                    {
                        ParseNUnitV3XmlTestSuite(reader, humanReadableOutput, nested || isFixture);
                    }
                    break;
                case XmlNodeType.EndElement:
                    if (reader.Name == "test-suite")
                    {
                        if (isFixture)
                        {
                            humanReadableOutput.WriteLine($"{testCaseName} {time} ms");
                        }

                        return;
                    }
                    break;
            }
        }

        throw new InvalidOperationException("Invalid XML: no test-suite end element");
    }
}
