// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;

namespace Microsoft.Arcade.Common
{
    public interface IFileSystem
    {
        void WriteToFile(string path, string content);

        // File
        bool FileExists(string path);
    }
}
