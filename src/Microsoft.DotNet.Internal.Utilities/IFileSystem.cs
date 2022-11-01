// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.DotNet.Internal.Utilities
{

    public interface IFileSystem
    {
        char DirectorySeparatorChar { get; }

        void WriteToFile(string path, string content);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);

        void DeleteDirectory(string path, bool recursive);

        string? GetFileName(string? path);

        string? GetDirectoryName(string? path);

        string? GetFileNameWithoutExtension(string? path);

        string? GetExtension(string? path);

        string PathCombine(string path1, string path2);

        void DeleteFile(string path);

        void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);

        Stream GetFileStream(string path, FileMode mode, FileAccess access);

        FileAttributes GetAttributes(string path);

        IFileInfo GetFileInfo(string path);
    }

    public interface IFileInfo
    {
        long Length { get; }
        bool Exists { get; }
    }

    public class FileInfoWrapper : IFileInfo
    {
        private readonly FileInfo _fileInfo;

        public FileInfoWrapper(string path)
        {
            _fileInfo = new FileInfo(path);
        }

        public long Length => _fileInfo.Length;

        public bool Exists => _fileInfo.Exists;
    }

}
