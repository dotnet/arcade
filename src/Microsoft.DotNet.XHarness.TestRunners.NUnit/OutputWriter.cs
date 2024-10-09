// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;

namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

/// <summary>
/// OutputWriter is an abstract class used to write test
/// results to a file in various formats. Specific
/// OutputWriters are derived from this class.
/// </summary>
internal abstract class OutputWriter
{
    /// <summary>
    /// Writes a test result to a file
    /// </summary>
    /// <param name="result">The result to be written</param>
    /// <param name="outputPath">Path to the file to which the result is written</param>
    public void WriteResultFile(IResultSummary result, string outputPath)
    {
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        WriteResultFile(result, writer);
    }

    /// <summary>
    /// Abstract method that writes a test result to a TextWriter
    /// </summary>
    /// <param name="result">The result to be written</param>
    /// <param name="writer">A TextWriter to which the result is written</param>
    public abstract void WriteResultFile(IResultSummary result, TextWriter writer);
}
