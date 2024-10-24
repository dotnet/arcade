// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.XHarness.Common.Logging;

public interface IReadableLog : ILog
{
    StreamReader GetReader();
}

public abstract class ReadableLog : Log, IReadableLog
{
    protected ReadableLog(string? description = null) : base(description) { }

    public abstract StreamReader GetReader();
}
