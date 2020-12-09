// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public interface IFileSystem
    {
        public void WriteXmlToFile(string path, XElement content);

        // File
        public bool FileExists(string path);
    }
}
