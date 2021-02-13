// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Arcade.Common
{
    public interface IFileSystem
    {
        void WriteToFile(string path, string content);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);
    }
}
