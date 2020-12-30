// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml.Linq;

namespace Microsoft.Arcade.Common
{
    public class FileSystem : IFileSystem
    {
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void WriteToFile(string path, string content)
        {
            string dirPath = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(path, content);
        }
    }
}
