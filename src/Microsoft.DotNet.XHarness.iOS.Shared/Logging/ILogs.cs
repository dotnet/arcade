// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public interface ILogs : IList<IFileBackedLog>, IDisposable
{
    string Directory { get; set; }

    // Create a new log backed with a file
    IFileBackedLog Create(string filename, string description, bool? timestamp = null);

    // Adds an existing file to this collection of logs.
    // If the file is not inside the log directory, then it's copied there.
    // 'path' must be a full path to the file.
    IFileBackedLog AddFile(string path);

    // Adds an existing file to this collection of logs.
    // If the file is not inside the log directory, then it's copied there.
    // 'path' must be a full path to the file.
    IFileBackedLog AddFile(string path, string name);

    // Create an empty file in the log directory and return the full path to the file
    string CreateFile(string path, string description);
    string CreateFile(string path, LogType type);
}
