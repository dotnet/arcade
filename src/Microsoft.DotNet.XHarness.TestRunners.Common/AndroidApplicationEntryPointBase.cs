// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Implementors should provide a text writter than will be used to
/// write the logging of the tests that are executed.
/// </summary>
public abstract class AndroidApplicationEntryPointBase : ApplicationEntryPoint
{
    public abstract TextWriter? Logger { get; }

    /// <summary>
    /// Implementors should provide a full path in which the final
    /// results of the test run will be written. This property must not
    /// return null.
    /// </summary>
    public abstract string TestsResultsFinalPath { get; }

    public override async Task RunAsync()
    {
        var options = ApplicationOptions.Current;
        using TextWriter? resultsFileMaybe = options.EnableXml ? File.CreateText(TestsResultsFinalPath) : null;
        await InternalRunAsync(options, Logger, resultsFileMaybe);
    }
}
