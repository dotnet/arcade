// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Common.Logging;

public interface IFileBackedLog : IReadableLog
{
    string FullPath { get; }
}

public abstract class FileBackedLog : ReadableLog, IFileBackedLog
{
    protected FileBackedLog(string? description = null) : base(description) { }

    public abstract string FullPath { get; }
}
