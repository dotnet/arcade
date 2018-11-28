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
        /// An array of XUnit project workitems containing the following metadata:
        /// - [Required] PublishDirectory: the publish output directory of the XUnit project
        /// - [Required] TargetPath: the output dll path
        /// - [Required] RuntimeTargetFramework: the target framework to run tests on
        /// - [Optional] Arguments: a string of arguments to be passed to the XUnit console runner
        /// The two required parameters will be automatically created if XUnitProject.Identity is set to the path of the XUnit csproj file
        /// </summary>
        [Required]
        public ITaskItem[] XUnitProjects { get; set; }

        /// <summary>
        /// The path to the dotnet executable on the Helix agent. Defaults to "dotnet"
        /// </summary>
        public string PathToDotnet { get; set; } = "dotnet";

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        public string XUnitArguments { get; set; }

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
            XUnitWorkItems = (await Task.WhenAll(XUnitProjects.Select(PrepareWorkItem))).Where(wi => wi != null).ToArray();
            return;
        }

        /// <summary>
        /// Prepares HelixWorkItem given xUnit project information.
        /// </summary>
        /// <param name="publishPath">The non-relative path to the publish directory.</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(ITaskItem xunitProject)
        {
            // Forces this task to run asynchronously
            await Task.Yield();
            
            if (!xunitProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
            {
                return null;
            }
            if (!xunitProject.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
            {
                return null;
            }
            if (!xunitProject.GetRequiredMetadata(Log, "RuntimeTargetFramework", out string runtimeTargetFramework))
            {
                return null;
            }

            xunitProject.TryGetMetadata("Arguments", out string arguments);

            string assemblyName = Path.GetFileName(targetPath);
            string driver = runtimeTargetFramework.Contains("core") ? $"{PathToDotnet} exec " : "";
            string runnerName = runtimeTargetFramework.Contains("core") ? "xunit.console.dll" : "xunit.console.exe";
            string correlationPayload = IsPosixShell ? "$HELIX_CORRELATION_PAYLOAD" : "%HELIX_CORRELATION_PAYLOAD%";
            string xUnitRunner = $"{correlationPayload}/tools/{runtimeTargetFramework}/{runnerName}";

            string command = $"{driver}{xUnitRunner}{(XUnitArguments != null ? " " + XUnitArguments : "")} {assemblyName} -xml testResults.xml {arguments}";

            Log.LogMessage($"Creating work item with properties Identity: {assemblyName}, PayloadDirectory: {publishDirectory}, Command: {command}");

            return new Build.Utilities.TaskItem(assemblyName, new Dictionary<string, string>()
            {
                { "Identity", assemblyName },
                { "PayloadDirectory", publishDirectory },
                { "Command", command }
            });
        }
    }
}
