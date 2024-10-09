// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class XmlResultParser : IResultParser
{
    private static readonly IHelpers s_helpers = new Helpers();
    private readonly Dictionary<XmlResultJargon, (IXmlResultParser Parser, ITestReportGenerator Generator)> _xmlFormatters = new()
    {
        { XmlResultJargon.TouchUnit, (new TouchUnitResultParser(), new TouchUnitTestReportGenerator()) },
        { XmlResultJargon.NUnitV2, (new NUnitV2ResultParser(), new NUnitV2TestReportGenerator()) },
        { XmlResultJargon.NUnitV3, (new NUnitV3ResultParser(), new NUnitV3TestReportGenerator()) },
        { XmlResultJargon.Trx, (new TrxResultParser(), new TrxTestReportGenerator()) },
        { XmlResultJargon.xUnit, (new XUnitResultParser(), new XUnitTestReportGenerator()) },
    };

    // test if the file is valid xml, or at least, that can be read it.
    public bool IsValidXml(string path, out XmlResultJargon type)
    {
        type = XmlResultJargon.Missing;
        if (!File.Exists(path))
        {
            return false;
        }

        using var stream = File.OpenText(path);
        return IsValidXml(stream, out type);
    }

    // test if the file is valid xml, or at least, that can be read it.
    public bool IsValidXml(TextReader stream, out XmlResultJargon type)
    {
        type = XmlResultJargon.Missing;

        string? line;
        while ((line = stream.ReadLine()) != null)
        { // special case when get got the tcp connection
            if (line.Contains("ping"))
            {
                continue;
            }

            if (line.Contains("test-run"))
            { // first element of the NUnitV3 test collection
                type = XmlResultJargon.NUnitV3;
                return true;
            }
            if (line.Contains("TouchUnitTestRun"))
            {
                type = XmlResultJargon.TouchUnit;
                return true;
            }
            if (line.Contains("test-results"))
            { // first element of the NUnitV3 test collection
                type = XmlResultJargon.NUnitV2;
                return true;
            }
            if (line.Contains("<assemblies>"))
            { // first element of the xUnit test collection
                type = XmlResultJargon.xUnit;
                return true;
            }
            if (line.Contains("<TestRun"))
            {
                type = XmlResultJargon.Trx;
                return true;
            }
        }

        return false;
    }

    public string GetXmlFilePath(string path, XmlResultJargon xmlType)
    {
        var fileName = Path.GetFileName(path);
        switch (xmlType)
        {
            case XmlResultJargon.TouchUnit:
            case XmlResultJargon.NUnitV2:
            case XmlResultJargon.NUnitV3:
                return path.Replace(fileName, $"nunit-{fileName}");
            case XmlResultJargon.xUnit:
                return path.Replace(fileName, $"xunit-{fileName}");
            default:
                return path;
        }
    }

    public void CleanXml(string source, string destination)
    {
        using (var reader = new StreamReader(source))
        using (var writer = new StreamWriter(destination))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("ping", StringComparison.Ordinal) || line.Contains("TouchUnitTestRun") || line.Contains("NUnitOutput") || line.Contains("<!--"))
                {
                    continue;
                }

                if (line == "") // remove white lines, some files have them
                {
                    continue;
                }

                if (line.Contains("TouchUnitExtraData")) // always last node in TouchUnit
                {
                    break;
                }

                writer.WriteLine(line);
            }
        }
    }

    public (string resultLine, bool failed) ParseResults(string source, XmlResultJargon xmlType, string? humanReadableReportDestination = null)
    {
        StreamWriter? writer = null;
        if (humanReadableReportDestination != null)
        {
            writer = new StreamWriter(humanReadableReportDestination, true);
        }

        var result = ParseResults(source, xmlType, writer);
        writer?.Dispose();
        return result;
    }

    public (string resultLine, bool failed) ParseResults(string source, XmlResultJargon xmlType, StreamWriter? humanReadableReportDestination = null)
    {
        var reader = new StreamReader(source);
        var parsedData = ("", true);

        if (_xmlFormatters.TryGetValue(xmlType, out var xmlFormatter))
        {
            parsedData = xmlFormatter.Parser.ParseXml(reader, humanReadableReportDestination);
        }

        return parsedData;
    }

    public void GenerateTestReport(TextWriter writer, string resultsPath, XmlResultJargon xmlType)
    {
        using var stream = new StreamReader(resultsPath);
        GenerateTestReport(writer, stream, xmlType);
    }

    public void GenerateTestReport(TextWriter writer, TextReader stream, XmlResultJargon xmlType)
    {
        var reader = XmlReader.Create(stream);

        if (_xmlFormatters.TryGetValue(xmlType, out var xmlFormatter))
        {
            xmlFormatter.Generator.GenerateTestReport(writer, reader);
        }
        else
        {
            writer.WriteLine($"<span style='padding-left: 15px;'>Could not parse {xmlType}: Not supported format.</span><br />");
        }
    }

    // get the file, parse it and add the attachments to the first node found
    public void UpdateMissingData(string source, string destination, string applicationName, IEnumerable<string> attachments)
    {
        // we could do this with a XmlReader and a Writer, but might be to complicated to get right, we pay with performance what we
        // cannot pay with brain cells.
        var doc = XDocument.Load(source);
        var attachmentsElement = new XElement("attachments");
        foreach (var path in attachments)
        {
            // we do not add a description, VSTS ignores that :/
            attachmentsElement.Add(new XElement("attachment",
                new XElement("filePath", path)));
        }

        var testSuitesElements = doc.Descendants().Where(e => e.Name == "test-suite" && e.Attribute("type")?.Value == "Assembly");
        if (!testSuitesElements.Any())
        {
            return;
        }

        // add the attachments to the first test-suite, this will add the attachmnets to it, which will be added to the test-run, the pipeline
        // SHOULD NOT merge runs, else this upload will be really hard to use. Also, just to one of them, else we have duplicated logs.
        testSuitesElements.FirstOrDefault().Add(attachmentsElement);

        foreach (var suite in testSuitesElements)
        {
            suite.SetAttributeValue("name", applicationName);
            suite.SetAttributeValue("fullname", applicationName); // docs say just name, but I've seen the fullname instead, docs usually lie
                                                                  // add also the attachments to all the failing tests, this will make the life of the person monitoring easier, since
                                                                  // he will see the logs directly from the attachment page
            var tests = suite.Descendants().Where(e => e.Name == "test-case" && e.Attribute("result").Value == "Failed");
            foreach (var t in tests)
            {
                t.Add(attachmentsElement);
            }
        }

        doc.Save(destination);
    }

    private void GenerateFailureXml(string destination, string title, string message, TextReader stderrReader, XmlResultJargon jargon)
    {
        var settings = new XmlWriterSettings { Indent = true };
        using (var stream = File.CreateText(destination))
        using (var xmlWriter = XmlWriter.Create(stream, settings))
        {
            xmlWriter.WriteStartDocument();

            if (_xmlFormatters.TryGetValue(jargon, out var xmlFormatters))
            {
                xmlFormatters.Generator.GenerateFailure(xmlWriter, title, message, stderrReader);
            }

            xmlWriter.WriteEndDocument();
        }
    }

    public void GenerateFailure(ILogs logs, string source, string appName, string? variation, string title, string message, string stderrPath, XmlResultJargon jargon)
    {
        using var stderrReader = new StreamReader(stderrPath);
        GenerateFailure(logs, source, appName, variation, title, message, stderrReader, jargon);
    }

    public void GenerateFailure(ILogs logs, string source, string appName, string? variation, string title,
        string message, TextReader stderrReader, XmlResultJargon jargon)
    {
        // VSTS does not provide a nice way to report build errors, create a fake
        // test result with a failure in the case the build did not work
        var failureLogXml = logs.Create($"vsts-nunit-{source}-{s_helpers.Timestamp}.xml", LogType.XmlLog.ToString());
        if (jargon == XmlResultJargon.NUnitV3)
        {
            var failureXmlTmp = logs.Create($"nunit-{source}-{s_helpers.Timestamp}.tmp", "Failure Log tmp");
            GenerateFailureXml(failureXmlTmp.FullPath, title, message, stderrReader, jargon);
            // add the required attachments and the info of the application that failed to install
            var failure_logs = Directory.GetFiles(logs.Directory).Where(p => !p.Contains("nunit")); // all logs but ourself
            UpdateMissingData(failureXmlTmp.FullPath, failureLogXml.FullPath, $"{appName} {variation}", failure_logs);
        }
        else
        {
            GenerateFailureXml(failureLogXml.FullPath, title, message, stderrReader, jargon);
        }
    }

    public static string GetVSTSFilename(string filename)
    {
        if (filename == null)
        {
            throw new ArgumentNullException(nameof(filename));
        }

        var dirName = Path.GetDirectoryName(filename);
        return dirName == null ? $"vsts-{Path.GetFileName(filename)}" : Path.Combine(dirName, $"vsts-{Path.GetFileName(filename)}");
    }
}
