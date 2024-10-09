// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class TrxResultParser : IXmlResultParser
{
    public class TrxTests : List<TrxTest>
    {
        public long Total;
        public long Executed;
        public long Passed;
        public long Failed;
        public long Error;
        public long Timeout;
        public long Aborted;
        public long Inconclusive;
        public long NotRunnable;
        public long NotExecuted;

    }

    public class TrxTest
    {
        public string? Outcome;
        public string? ClassName;
        public string? TestName;
        public TimeSpan? Duration;
        public string? Message;
    }

    public (string resultLine, bool failed) ParseXml(TextReader stream, TextWriter? humanReadableOutput)
    {
        using var reader = XmlReader.Create(stream);
        var tests = ParseTrxXml(reader);
        var resultLine = $"Tests run: {tests.Total} Passed: {tests.Passed} Inconclusive: {tests.Inconclusive} Failed: {tests.Failed + tests.Error} Ignored: {tests.NotRunnable}";

        if (humanReadableOutput != null)
        {
            foreach (var groupedByClass in tests.GroupBy(v => v.ClassName).OrderBy(v => v.Key))
            {
                var className = groupedByClass.Key;
                var totalDuration = TimeSpan.FromTicks(groupedByClass.Select(v => v.Duration?.Ticks ?? 0).Sum());
                humanReadableOutput.WriteLine(className);
                foreach (var test in groupedByClass)
                {
                    humanReadableOutput.Write('\t');
                    switch (test.Outcome)
                    {
                        case "Passed":
                            humanReadableOutput.Write("[PASS]");
                            break;
                        default:
                            humanReadableOutput.Write($"[UNKNOWN ({test.Outcome})]");
                            break;
                    }
                    humanReadableOutput.Write(' ');
                    humanReadableOutput.Write(test.TestName);
                    humanReadableOutput.Write(": ");
                    humanReadableOutput.Write(test.Duration?.ToString());
                    humanReadableOutput.WriteLine();
                }

                humanReadableOutput.WriteLine($"{className} {totalDuration}");
            }

            humanReadableOutput.WriteLine(resultLine);
        }

        return (resultLine, !(tests.Error == 0 && tests.Aborted == 0 && tests.Timeout == 0 && tests.Failed == 0));
    }

    public static TrxTests ParseTrxXml(XmlReader reader)
    {
        var rv = new TrxTests();
        var tests = new Dictionary<string, TrxTest>();
        TrxTest? lastTest = null;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.Name)
            {
                case "Counters":
                    long.TryParse(reader["total"], out rv.Total);
                    long.TryParse(reader["executed"], out rv.Executed);
                    long.TryParse(reader["passed"], out rv.Passed);
                    long.TryParse(reader["failed"], out rv.Failed);
                    long.TryParse(reader["error"], out rv.Error);
                    long.TryParse(reader["timeout"], out rv.Timeout);
                    long.TryParse(reader["aborted"], out rv.Aborted);
                    long.TryParse(reader["inconclusive"], out rv.Inconclusive);
                    long.TryParse(reader["notRunnable"], out rv.NotRunnable);
                    long.TryParse(reader["notExecuted"], out rv.NotExecuted);
                    break;

                case "UnitTestResult":
                    {
                        var testId = reader["testId"];
                        var outcome = reader["outcome"];
                        var test = new TrxTest { Outcome = outcome };
                        if (TimeSpan.TryParse(reader["duration"], out var duration))
                        {
                            test.Duration = duration;
                        }

                        tests[testId] = test;
                        rv.Add(test);
                        lastTest = test;
                        break;
                    }

                case "Message":
                    if (lastTest != null)
                    {
                        reader.Read();
                        lastTest.Message = reader.Value;
                    }
                    break;

                case "UnitTest":
                    {
                        var id = reader["id"];
                        var test = tests[id];
                        while (reader.Read() && !(reader.NodeType == XmlNodeType.Element && reader.Name == "TestMethod"))
                        {
                            ;
                        }

                        test.ClassName = reader["className"];
                        test.TestName = reader["name"];
                        break;
                    }

                default:
                    break;
            }
        }
        return rv;
    }
}
