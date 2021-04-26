// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO;

namespace Microsoft.Arcade.Common
{
    public interface IFileSystem
    {
        void WriteToFile(string path, string content);

        string ReadFromFile(string path);

        string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);
    }
}
