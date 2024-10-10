// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Xml;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class TouchUnitResultParser : IXmlResultParser
{
    public (string resultLine, bool failed) ParseXml(TextReader stream, TextWriter? humanReadableOutput)
    {
        long total, errors, failed, notRun, inconclusive, ignored, skipped, invalid;
        total = errors = failed = notRun = inconclusive = ignored = skipped = invalid = 0L;

        using (var reader = XmlReader.Create(stream))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "test-results")
                {
                    long.TryParse(reader["total"], out total);
                    long.TryParse(reader["errors"], out errors);
                    long.TryParse(reader["failures"], out failed);
                    long.TryParse(reader["not-run"], out notRun);
                    long.TryParse(reader["inconclusive"], out inconclusive);
                    long.TryParse(reader["ignored"], out ignored);
                    long.TryParse(reader["skipped"], out skipped);
                    long.TryParse(reader["invalid"], out invalid);
                }
                if (humanReadableOutput != null && reader.NodeType == XmlNodeType.Element && reader.Name == "TouchUnitExtraData")
                {
                    // move fwd to get to the CData
                    if (reader.Read())
                    {
                        humanReadableOutput.Write(reader.Value);
                    }
                }
            }
        }
        var passed = total - errors - failed - notRun - inconclusive - ignored - skipped - invalid;
        var resultLine = $"Tests run: {total} Passed: {passed} Inconclusive: {inconclusive} Failed: {failed + errors} Ignored: {ignored + skipped + invalid}";
        humanReadableOutput?.WriteLine(resultLine);
        return (resultLine, errors != 0 || failed != 0);
    }
}
