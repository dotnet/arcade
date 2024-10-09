// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public class Logs : List<IFileBackedLog>, ILogs
{
    private readonly IHelpers _helpers = new Helpers();

    public string Directory { get; set; }

    public Logs(string directory)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    public IFileBackedLog Create(string filename, string description, bool? timestamp = null)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var rv = new LogFile(description, Path.GetFullPath(Path.Combine(Directory, filename)));
        if (timestamp.HasValue)
        {
            rv.Timestamp = timestamp.Value;
        }

        Add(rv);
        return rv;
    }

    // Adds an existing file to this collection of logs.
    // If the file is not inside the log directory, then it's copied there.
    // 'path' must be a full path to the file.
    public IFileBackedLog AddFile(string path) => AddFile(path, Path.GetFileName(path));

    // Adds an existing file to this collection of logs.
    // If the file is not inside the log directory, then it's copied there.
    // 'path' must be a full path to the file.
    public IFileBackedLog AddFile(string path, string name)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!path.StartsWith(Directory, StringComparison.Ordinal))
        {
            var newPath = Path.Combine(Directory, Path.GetFileNameWithoutExtension(path) + "-" + _helpers.Timestamp + Path.GetExtension(path));
            File.Copy(path, newPath, true);
            path = newPath;
        }

        var log = new LogFile(name, path, true);
        Add(log);
        return log;
    }

    // Create an empty file in the log directory and return the full path to the file
    public string CreateFile(string path, string description)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        using (var rv = new LogFile(description, Path.Combine(Directory, path), false))
        {
            Add(rv);
            return rv.FullPath;
        }
    }

    public string CreateFile(string path, LogType type) => CreateFile(path, type.ToString());

    public void Dispose()
    {
        foreach (var log in this)
        {
            log.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
