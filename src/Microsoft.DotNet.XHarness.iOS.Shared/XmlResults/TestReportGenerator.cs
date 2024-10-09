// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Xml;

using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public interface ITestReportGenerator
{
    void GenerateTestReport(TextWriter writer, XmlReader reader);

    void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr);
}

public abstract class TestReportGenerator : ITestReportGenerator
{
    public abstract void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr);

    public abstract void GenerateTestReport(TextWriter writer, XmlReader reader);

    protected static void WriteAttributes(XmlWriter writer, params (string name, string data)[] attrs)
    {
        foreach (var (name, data) in attrs)
        {
            writer.WriteAttributeString(name, data);
        }
    }

    protected static void WriteFailure(XmlWriter writer, string message, TextReader? stderr = null)
    {
        writer.WriteStartElement("failure");
        writer.WriteStartElement("message");
        writer.WriteCDataSafe(message);
        writer.WriteEndElement(); // message
        if (stderr != null)
        {
            writer.WriteStartElement("stack-trace");
            writer.WriteCDataSafe(stderr.ReadToEnd());
            writer.WriteEndElement(); //stack trace
        }
        writer.WriteEndElement(); // failure
    }
}
