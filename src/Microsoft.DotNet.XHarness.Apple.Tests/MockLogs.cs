using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;

namespace Microsoft.DotNet.XHarness.Apple.Tests;

#nullable disable
public class MockLogs : List<IFileBackedLog>, ILogs
{
    public string Directory { get; set; } = "/tmp/logs/";

    public IFileBackedLog AddFile(string path) => AddFile(path, path);

    public IFileBackedLog AddFile(string path, string name)
    {
        var log = Mock.Of<IFileBackedLog>(x => x.FullPath == path && x.Description == name);
        Add(log);
        return log;
    }

    public string CreateFile(string path, string description)
    {
        var log = Mock.Of<IFileBackedLog>(x => x.FullPath == path && x.Description == description);
        Add(log);
        return Directory + path;
    }

    public IFileBackedLog Create(string filename, string name, bool? timestamp = null) => AddFile(filename, name);

    public string CreateFile(string path, LogType type) => CreateFile(path, type.ToString());

    public void Dispose() { }
}
