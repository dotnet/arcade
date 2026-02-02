// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Common;

#nullable enable
namespace Microsoft.Arcade.Test.Common
{
    public class MockFileSystem : IFileSystem
    {
        private const string BinaryPrefix = "base64:";

        #region File system state

        public HashSet<string> Directories { get; }    
        
        public Dictionary<string, string> Files { get; }

        public List<string> RemovedFiles { get; } = new();

        #endregion

        public string DirectorySeparator { get; }

        public MockFileSystem(
            Dictionary<string, string>? files = null,
            IEnumerable<string>? directories = null,
            string directorySeparator = "/")
        {
            Directories = new(directories ?? new string[0]);
            Files = files ?? new();
            DirectorySeparator = directorySeparator;
        }

        #region IFileSystem implementation

        public void CreateDirectory(string path) => Directories.Add(path);

        public bool DirectoryExists(string path) => Directories.Contains(path);

        public bool FileExists(string path) => Files.ContainsKey(path);

        public void DeleteFile(string path)
        {
            Files.Remove(path);
            RemovedFiles.Add(path);
        }

        public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

        public string? GetFileName(string? path) => Path.GetFileName(path);

        public string? GetFileNameWithoutExtension(string? path) => Path.GetFileNameWithoutExtension(path);

        public string? GetExtension(string? path) => Path.GetExtension(path);

        public string PathCombine(string path1, string path2) => path1 + DirectorySeparator + path2;

        public string PathCombine(string path1, string path2, string path3) => path1 + DirectorySeparator + path2 + DirectorySeparator + path3;

        public void WriteToFile(string path, string content)
        {
            Files[path] = content;
        }

        public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
        {
            Files[destFileName] = Files[sourceFileName];
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access)
        {
            // Always use MockFileStream which handles both read and write correctly
            return new MockFileStream(this, path, mode, access);
        }

        public FileAttributes GetAttributes(string path)
        {
            var attributes = FileAttributes.Normal;

            if (Directories.Contains(path))
            {
                attributes |= FileAttributes.Directory;
            }

            return  attributes;
        }

        public string GetFullPath(string path) => path;

        public string GetRelativePath(string basePath, string targetPath)
        {
            if (targetPath.IndexOf(basePath) != 0)
            {
                throw new ArgumentException("targetPath is not relative to basePath");
            }

            return targetPath.Replace(basePath, "").TrimStart('/', '\\');
        }

        public byte[] ReadAllBytes(string path)
        {
            if (!Files.ContainsKey(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            string content = Files[path];
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<byte>();
            }

            // Binary-safe: bytes are stored as base64 in the existing string dictionary.
            // Text-based tests typically use WriteToFile / Files directly.
            if (content.StartsWith(BinaryPrefix, StringComparison.Ordinal))
            {
                return Convert.FromBase64String(content.Substring(BinaryPrefix.Length));
            }

            // Treat existing content as plain text.
            return System.Text.Encoding.UTF8.GetBytes(content);
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            Files[path] = bytes.Length == 0 ? string.Empty : BinaryPrefix + Convert.ToBase64String(bytes);
        }

        public long GetFileLength(string path)
        {
            if (!Files.ContainsKey(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            string content = Files[path];
            if (string.IsNullOrEmpty(content))
            {
                return 0;
            }

            if (content.StartsWith(BinaryPrefix, StringComparison.Ordinal))
            {
                return Convert.FromBase64String(content.Substring(BinaryPrefix.Length)).LongLength;
            }

            return System.Text.Encoding.UTF8.GetByteCount(content);
        }

        #endregion

        /// <summary>
        /// Allows to read and write to a stream that will end up in the MockFileSystem.
        /// </summary>
        private class MockFileStream : MemoryStream
        {
            private readonly MockFileSystem _fileSystem;
            private readonly string _path;
            private readonly FileAccess _access;
            private bool _disposed = false;

            public MockFileStream(MockFileSystem fileSystem, string path, FileMode mode, FileAccess access)
            {
                _fileSystem = fileSystem;
                _path = path;
                _access = access;

                // Initialize the stream based on mode and existing file content
                if (mode == FileMode.Open || mode == FileMode.Append)
                {
                    if (fileSystem.FileExists(path))
                    {
                        byte[] existingContent = fileSystem.ReadAllBytes(path);
                        Write(existingContent, 0, existingContent.Length);
                        
                        if (mode == FileMode.Open)
                        {
                            Seek(0, SeekOrigin.Begin); // Reset to beginning for read
                        }
                        // For Append mode, position is already at the end
                    }
                    else if (mode == FileMode.Open)
                    {
                        throw new FileNotFoundException($"File not found: {path}");
                    }
                }
                else if (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.Truncate)
                {
                    // Start with an empty stream
                    if (mode == FileMode.CreateNew && fileSystem.FileExists(path))
                    {
                        throw new IOException($"File already exists: {path}");
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                // Flush to file system if we have write access
                if (!_disposed && (_access == FileAccess.Write || _access == FileAccess.ReadWrite))
                {
                    _disposed = true;
                    _fileSystem.WriteAllBytes(_path, ToArray());
                }
                
                base.Dispose(disposing);
            }
        }
    }
}
