using System;
using System.IO;

namespace SignTool
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
