// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.SignTool
{
    internal interface IHost
    {
        bool DirectoryExists(string directoryName);
        string GetFolderPath(Environment.SpecialFolder folder);
        string GetEnvironmentVariable(string variable);
    }

    internal sealed class StandardHost : IHost
    {
        internal static StandardHost Instance { get; } = new StandardHost();

        public bool DirectoryExists(string directoryName) => Directory.Exists(directoryName);
        public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
        public string GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
    }
}
