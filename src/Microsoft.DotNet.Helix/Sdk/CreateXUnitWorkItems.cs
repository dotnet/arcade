using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems given xUnit project publish information
    /// </summary>
    public class CreateXUnitWorkItems : Build.Utilities.Task
    {
        /// <summary>
        /// An array of XUnit project publish directories
        /// </summary>
        [Required]
        public string[] XUnitDllDirectories { get; set; }

        /// <summary>
        /// An array of paths to the DLLs created by an XUnit project
        /// </summary>
        [Required]
        public string[] XUnitDllPaths { get; set; }

        /// <summary>
        /// The framework for the XUnit console runner
        /// Should match the name of the folder in the xunit.console.runner nupkg
        /// </summary>
        [Required]
        public string XUnitTargetFramework { get; set; }

        /// <summary>
        /// A list of arguments to be passed to xUnit.
        /// Either specify one set per xUnit project (in order of project) or specify a single set to be applied to all projects
        /// </summary>
        public string[] XUnitArguments { get; set; }

        /// <summary>
        /// The path to the dotnet executable on the Helix agent. Defaults to "dotnet"
        /// </summary>
        public string PathToDotnet { get; set; }

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        private Dictionary<string, string> _directoriesToPathMap;
        private Dictionary<string, string> _directoriesToArgumentsMap;

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItem creation per
        /// provided xUnit project data.
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation per provided xUnit project data.</returns>
        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// The asynchronous execution method for this MSBuild task which verifies the integrity of required properties
        /// and validates their formatting, specifically determining whether the provided xUnit project data have a 
        /// one-to-one mapping. It then creates this mapping before asynchronously preparing the HelixWorkItem TaskItem
        /// objects via the PrepareWorkItem method.
        /// </summary>
        /// <returns></returns>
        private async Task ExecuteAsync()
        {
            if (XUnitDllDirectories is null)
            {
                Log.LogError("Required metadata XUnitDllDirectories is null");
                return;
            }
            if (XUnitDllPaths is null)
            {
                Log.LogError("Required metadata XUnitDllPaths is null");
                return;
            }

            if (XUnitDllDirectories.Length < 1)
            {
                Log.LogError("No XUnit Projects found");
                return;
            }
            else if (XUnitDllDirectories.Length > XUnitDllPaths.Length)
            {
                Log.LogError("Not all XUnit projects produced assemblies");
                return;
            }
            else if (XUnitDllDirectories.Length < XUnitDllPaths.Length)
            {
                Log.LogError("More XUnit assemblies were found than projects");
                return;
            }

            if ((XUnitArguments is null ? 0 : XUnitArguments.Length) != 0 && XUnitArguments.Length != 1 && XUnitArguments.Length != XUnitDllDirectories.Length)
            {
                Log.LogError($"Number of XUnit arguments does not match number of XUnit projects: {XUnitArguments.Length} sets of arguments were provided for {XUnitDllDirectories.Length} projects");
            }

            _directoriesToPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _directoriesToArgumentsMap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < XUnitDllDirectories.Length; i++)
            {
                if (_directoriesToPathMap.ContainsKey(XUnitDllDirectories[i]))
                {
                    Log.LogError("Two identical publish paths were provided");
                    return;
                }
                else
                {
                    _directoriesToPathMap.Add(XUnitDllDirectories[i], XUnitDllPaths[i]);
                    if (XUnitArguments.Length == 0)
                    {
                        _directoriesToArgumentsMap.Add(XUnitDllDirectories[i], "");
                    }
                    else if (XUnitArguments.Length == 1)
                    {
                        _directoriesToArgumentsMap.Add(XUnitDllDirectories[i], XUnitArguments[0]);
                    }
                    else
                    {
                        _directoriesToArgumentsMap.Add(XUnitDllDirectories[i], XUnitArguments[i]);
                    }
                }
            }

            XUnitWorkItems = await Task.WhenAll(XUnitDllDirectories.Select(PrepareWorkItem));
            return;
        }

        /// <summary>
        /// Prepares HelixWorkItem given xUnit project information.
        /// </summary>
        /// <param name="publishPath">The non-relative path to the publish directory.</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(string publishPath)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            string assemblyName = Path.GetFileNameWithoutExtension(_directoriesToPathMap[publishPath]);
            string dotNetPath =  XUnitTargetFramework.Contains("core") ? (string.IsNullOrEmpty(PathToDotnet) ? "dotnet exec " : $"{PathToDotnet} exec ") : "";

            string xunitConsoleRunner = "";
            if (IsPosixShell)
            {
                xunitConsoleRunner = "$XUNIT_CONSOLE_RUNNER";
            }
            else
            {
                xunitConsoleRunner = "%XUNIT_CONSOLE_RUNNER%";
            }

            string command = $"{dotNetPath}{xunitConsoleRunner} {assemblyName}.dll -xml {Guid.NewGuid()}-testResults.xml {_directoriesToArgumentsMap[publishPath]}";

            Log.LogMessage($"Creating work item with properties Identity: {assemblyName}, PayloadDirectory: {publishPath}, Command: {command}");

            ITaskItem workItem = new Build.Utilities.TaskItem(assemblyName, new Dictionary<string, string>()
            {
                { "Identity", assemblyName },
                { "PayloadDirectory", publishPath },
                { "Command", command }
            });
            return workItem;
        }
    }
}
