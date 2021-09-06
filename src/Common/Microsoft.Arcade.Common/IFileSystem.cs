// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.IO;

namespace Microsoft.Arcade.Common
{
    public interface IFileSystem
    {
        void WriteToFile(string path, string content);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);

        string? GetFileName(string? path);

        string? GetDirectoryName(string? path);

        string? GetFileNameWithoutExtension(string? path);

        string? GetExtension(string? path);

        string PathCombine(string path1, string path2);

        void DeleteFile(string path);

        void FileCopy(string sourceFileName, string destFileName);

        Stream GetFileStream(string path, FileMode mode, FileAccess access);

        FileAttributes GetAttributes(string path);
    }
}
