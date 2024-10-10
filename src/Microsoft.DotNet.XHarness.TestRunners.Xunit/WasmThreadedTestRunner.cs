// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class WasmThreadedTestRunner : XUnitTestRunner
{
    public WasmThreadedTestRunner(LogWriter logger) : base(logger)
    {
        TestStagePrefix = string.Empty;
        ShowFailureInfos = false;
    }

    protected override string ResultsFileName { get => string.Empty; set => throw new InvalidOperationException("This runner outputs its results to stdout."); }

    public override Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        OnInfo("Using threaded Xunit runner");
        return base.Run(testAssemblies);
    }

    public override void WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
        => WasmXmlResultWriter.WriteOnSingleLine(AssembliesElement);
}
