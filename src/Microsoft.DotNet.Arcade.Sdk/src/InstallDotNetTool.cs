using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Arcade.Sdk
{
    /// <summary>
    /// Task that installs a .NET tool in a given folder.
    /// Handles parallel builds that install the same tool.
    /// </summary>
    public class InstallDotNetTool : Task
    {
        /// <summary>
        /// After this period, we just give up on trying.
        /// </summary>
        private static readonly TimeSpan s_installationTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The name of the tool to install (same as the NuGet package name, e.g. Microsoft.DotNet.XHarness.CLI).
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Directory where the tool will be installed.
        /// </summary>
        [Required]
        public string DestinationPath { get; set; }

        /// <summary>
        /// Version of the tool to install.
        /// Must not contain * so that we can make sure the right version is installed.
        /// </summary>
        [Required]
        public string Version { get; set; }

        /// <summary>
        /// Optional path to dotnet.exe to use.
        /// </summary>
        public string DotnetPath { get; set; }

        /// <summary>
        /// Source to install the tool from.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Location of where the tool was installed (including the version).
        /// </summary>
        [Output]
        public string ToolPath { get; set; }

        private string LockFilePath => Path.Combine(DestinationPath, Name + ".lock");

        public override bool Execute()
        {
            if (Version.Contains("*"))
            {
                Log.LogError("InstallDotNetTool task doesn't accept * in the version");
                return false;
            }

            // We install the tool in [dest]/[name]/[version] because if we tried to install 2 versions in the same dir,
            // dotnet tool install would fail.
            var version = Version.ToLowerInvariant();
            ToolPath = Path.Combine(DestinationPath, Name, Version);

            if (!Directory.Exists(ToolPath))
            {
                Directory.CreateDirectory(DestinationPath);
            }

            string storePath = Path.Combine(ToolPath, ".store", Name.ToLowerInvariant());
            string versionInstallPath = Path.Combine(storePath, version);

            if (IsOrIsBeingInstalled(versionInstallPath))
            {
                return true;
            }

            FileStream lockFile;
            try
            {
                lockFile = new FileStream(LockFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                // Other process acquired the lock before we did
                return IsOrIsBeingInstalled(versionInstallPath);
            }

            try
            {
                using (lockFile)
                {
                    Log.LogMessage($"Installing {Name} v{Version}...");

                    var args = new List<string>()
                    {
                        "tool",
                        "install",
                        "--version",
                        Version,
                        "--tool-path",
                        ToolPath,
                    };

                    if (!string.IsNullOrEmpty(Source))
                    {
                        args.Add("--add-source");
                        args.Add(Source);
                    }

                    args.Add(Name);

                    Command command = Command.Create(string.IsNullOrEmpty(DotnetPath) ? "dotnet" : DotnetPath, args);
                    CommandResult result = command.Execute();

                    if (result.ExitCode != 0)
                    {
                        return Directory.Exists(versionInstallPath);
                    }

                    Log.LogMessage($"{Name} v{Version} installed");

                    return true;
                }
            }
            finally
            {
                File.Delete(LockFilePath);
            }
        }

        private bool IsOrIsBeingInstalled(string pathToVersion)
        {
            var stopwatch = Stopwatch.StartNew();
            bool reported = false;

            while (!Directory.Exists(pathToVersion) && IsInstallationInProgress())
            {
                if (!reported)
                {
                    Log.LogMessage($"{Name} is being installed by other process. Waiting...");
                    reported = true;
                }

                if (stopwatch.Elapsed > s_installationTimeout)
                {
                    Log.LogError($"Concurrent installation of {Name} didn't finish in time.");
                    return false;
                }

                Thread.Sleep(500);
            }

            if (Directory.Exists(pathToVersion))
            {
                Log.LogMessage($"{Name} v{Version} was installed by other process successfully");
                return true;
            }

            return false;
        }

        private bool IsInstallationInProgress()
        {
            if (!File.Exists(LockFilePath))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.Open(LockFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            }
            catch (IOException)
            {
                // The file is used by other process or does not exist
                return true;
            }

            // File exists and is not used by other process
            return false;
        }
    }
}
