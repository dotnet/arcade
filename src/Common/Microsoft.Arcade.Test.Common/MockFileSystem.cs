// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Common;

#nullable enable
namespace Microsoft.Arcade.Test.Common
{
    public class MockFileSystem : IFileSystem
    {
        public HashSet<string> Files { get; }

        public HashSet<string> Directories { get; }    
        
        public Dictionary<string, string> FileContents { get; }

        public List<string> RemovedDirectories { get; } = new();

        public List<string> RemovedFiles { get; } = new();

        public MockFileSystem(
            IEnumerable<string>? files = null,
            IEnumerable<string>? directories = null,
            Dictionary<string, string>? fileContents = null)
        {
            Files = new(files ?? new string[0]);
            Directories = new(directories ?? new string[0]);
            FileContents = fileContents ?? new();
        }

        #region IFileSystem implementation

        public void CreateDirectory(string path) => Directories.Add(path);

        public bool DirectoryExists(string path) => Directories.Contains(path);

        public bool FileExists(string path) => Files.Contains(path);

        public void DeleteFile(string path) => Files.Remove(path);

        public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

        public string? GetFileName(string? path) => Path.GetFileName(path);

        public string? GetFileNameWithoutExtension(string? path) => Path.GetFileNameWithoutExtension(path);

        public string? GetExtension(string? path) => Path.GetExtension(path);

        public string PathCombine(string path1, string path2) => Path.Combine(path1, path2);

        public void WriteToFile(string path, string content)
        {
            FileContents[path] = content;
            Files.Add(path);
        }

        public void FileCopy(string sourceFileName, string destFileName) => Files.Add(destFileName);

        public Stream GetFileStream(string path, FileMode mode, FileAccess access)
            => FileExists(path) ? new MemoryStream() : new MockFileStream(this, path);

        #endregion

        private class MockFileStream : MemoryStream
        {
            private readonly MockFileSystem _fileSystem;
            private readonly string _path;

            public MockFileStream(MockFileSystem fileSystem, string path)
                : base(fileSystem.FileExists(path) ? System.Text.Encoding.UTF8.GetBytes(fileSystem.FileContents[path]) : new byte[1024])
            {
                _fileSystem = fileSystem;
                _path = path;
            }

            public new void Dispose()
            {
                // flush file to our system
                using var sr = new StreamReader(this);
                _fileSystem.WriteToFile(_path, sr.ReadToEnd());
            }
        }
    }
}
