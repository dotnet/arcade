// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

/// <summary>
/// Interface that represents an object that know how to parse results and generate timeout/crash/build errors so
/// that CIs like VSTS and helix can parse them.
/// </summary>
public interface IResultParser
{
    /// <summary>
    /// Generates an XML result that will consider to be an error by the CI. Allows to catch errors in cases in which we are not talking about a test
    /// failure per se but the situation in which the app could not be built, timeout or crashed.
    /// </summary>
    void GenerateFailure(ILogs logs, string source, string appName, string? variation, string title, string message, string stderrPath, XmlResultJargon jargon);

    /// <summary>
    /// Generates an XML result that will consider to be an error by the CI. Allows to catch errors in cases in which we are not talking about a test
    /// failure per se but the situation in which the app could not be built, timeout or crashed.
    /// </summary>
    void GenerateFailure(ILogs logs, string source, string appName, string variation, string title, string message, TextReader stderrReader, XmlResultJargon jargon);

    /// <summary>
    /// Updates given xml result to contain a list of attachments. This is useful for CI to be able to add logs as part of the attachments of a failing test.
    /// </summary>
    void UpdateMissingData(string source, string destination, string applicationName, IEnumerable<string> attachments);

    /// <summary>
    /// Ensures that the given path contains a valid xml result and set the type of xml jargon found in the file.
    /// </summary>
    bool IsValidXml(string path, out XmlResultJargon type);

    /// <summary>
    /// Ensures that the given path contains a valid xml result and set the type of xml jargon found in the file.
    /// </summary>
    bool IsValidXml(TextReader stream, out XmlResultJargon type);

    /// <summary>
    /// Takes an XML file and removes any extra data that makes the test result not to be a pure xml result for the given jargon.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    void CleanXml(string source, string destination);

    /// <summary>
    /// Returns the path to be used for the given jargon.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="xmlType"></param>
    /// <returns></returns>
    string GetXmlFilePath(string path, XmlResultJargon xmlType);

    /// <summary>
    /// Parses the xml of the given jargon and returns a result line with the summary of what was parsed.
    /// If destination is provided, creates a human readable report.
    /// </summary>
    /// <param name="source">File that will be read</param>
    /// <param name="xmlType">Jargon of the source file</param>
    /// <param name="humanReadableReportDestination">If provided, will contain human readable result</param>
    /// <returns>A result line with the summary of what was parsed and an over all result</returns>
    (string resultLine, bool failed) ParseResults(string source, XmlResultJargon xmlType, string? humanReadableReportDestination = null);

    /// <summary>
    /// Parses the xml of the given jargon and returns a result line with the summary of what was parsed.
    /// If destination is provided, creates a human readable report.
    /// </summary>
    /// <param name="source">File that will be read</param>
    /// <param name="xmlType">Jargon of the source file</param>
    /// <param name="humanReadableReportDestination">If provided, will contain human readable result</param>
    /// <returns>A result line with the summary of what was parsed and an over all result</returns>
    (string resultLine, bool failed) ParseResults(string source, XmlResultJargon xmlType, StreamWriter? textWriter = null);

    /// <summary>
    /// Generates a human readable test report.
    /// </summary>
    void GenerateTestReport(TextWriter writer, string resultsPath, XmlResultJargon xmlType);
}
