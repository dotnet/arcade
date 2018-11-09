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
        /// The path to the dotnet executable on the Helix agent. Defaults to "dotnet"
        /// </summary>
        public string PathToDotnet { get; set; }

        private Dictionary<string, string> _directoriesToPathMap;

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

            _directoriesToPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            string dotNetPath = string.IsNullOrEmpty(PathToDotnet) ? "dotnet" : PathToDotnet;
            string command = $"{dotNetPath} xunit.console.dll {assemblyName}.dll -xml testResults.xml";

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
