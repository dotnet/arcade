// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.DotNet.XHarness.Common.Logging;

public interface ILog : IDisposable
{
    string? Description { get; set; }
    bool Timestamp { get; set; }
    Encoding Encoding { get; }
    void Write(byte[] buffer, int offset, int count);
    void Write(string value);
    void WriteLine(string value);
    void WriteLine(string format, params object[] args);
    void Flush();
}
