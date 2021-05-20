using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.DotNet.Arcade.Sdk
{
    /// <summary>
    /// Task that installs a .NET tool in a given folder.
    /// Handles parallel builds that install the same tool.
    /// </summary>
    public class InstallDotNetTool : MSBuildTaskBase
    {
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

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddTransient<ICommandFactory, CommandFactory>();
            collection.TryAddTransient<IFileSystem, FileSystem>();
            collection.TryAddTransient<IHelpers, Helpers>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(ICommandFactory commandFactory, IFileSystem fileSystem, IHelpers helpers)
        {
            if (Version.Contains("*"))
            {
                Log.LogError("InstallDotNetTool task doesn't accept * in the version");
                return false;
            }

            // We install the tool in [dest]/[name]/[version] because if we tried to install 2 versions in the same dir,
            // `dotnet tool install` would fail.
            var version = Version.ToLowerInvariant();
            ToolPath = Path.Combine(DestinationPath, Name, Version);

            if (!fileSystem.DirectoryExists(ToolPath))
            {
                fileSystem.CreateDirectory(DestinationPath);
            }

            string versionInstallPath = Path.Combine(ToolPath, ".store", Name.ToLowerInvariant(), version);

            return helpers.DirectoryMutexExec(() =>
            {
                if (fileSystem.DirectoryExists(versionInstallPath))
                {
                    Log.LogMessage($"{Name} v{Version} is already installed");
                    return true;
                }

                return InstallTool(commandFactory);
            }, ToolPath);
        }

        private bool InstallTool(ICommandFactory commandFactory)
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

            var command = commandFactory.Create(string.IsNullOrEmpty(DotnetPath) ? "dotnet" : DotnetPath, args);
            CommandResult result = command.Execute();

            if (result.ExitCode != 0)
            {
                Log.LogError(
                    $"Failed to install the dotnet tool. Installation exited with {result.ExitCode}. " +
                    Environment.NewLine + (string.IsNullOrEmpty(result.StdErr) ? result.StdOut : result.StdErr));
                return false;
            }

            Log.LogMessage($"{Name} v{Version} installed");
            return true;
        }
    }
}
