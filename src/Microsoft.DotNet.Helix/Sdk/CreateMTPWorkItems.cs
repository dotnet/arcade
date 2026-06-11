// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for test projects that target
    /// Microsoft.Testing.Platform (MTP). Such projects ship as self-hosting executables
    /// (a Main entry point is emitted by Microsoft.Testing.Platform.MSBuild) and can be
    /// run directly with 'dotnet exec'. This applies to MSTest 4.x, xUnit v3 with MTP,
    /// NUnit with MTP, TUnit, and any custom MTP-based test framework.
    /// </summary>
    public class CreateMTPWorkItems : BaseTask
    {
        /// <summary>
        /// An array of MTP project work items containing the following metadata:
        /// - [Required] PublishDirectory: the publish output directory of the test project
        /// - [Required] TargetPath: the output dll path
        /// - [Optional] Arguments: a string of arguments to be passed to the test executable
        ///   *after* the auto-injected reporter flags
        /// The two required parameters are populated automatically by MTPRunner.targets when
        /// MTPProject.Identity is set to the path of the test csproj file.
        /// </summary>
        [Required]
        public ITaskItem[] MTPProjects { get; set; }

        /// <summary>
        /// The path to the dotnet executable on the Helix agent. Defaults to "dotnet".
        /// </summary>
        public string PathToDotnet { get; set; } = "dotnet";

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in
        /// Microsoft.DotNet.Helix.Sdk.MonoQueue.targets. Currently unused (the dotnet exec
        /// command is identical on every shell) but accepted for symmetry with the other
        /// Create*WorkItems tasks and to allow future shell-specific tweaks without an
        /// API break.
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        /// <summary>
        /// Optional timeout for all created work items.
        /// Accepts any value parseable by <see cref="TimeSpan.TryParse(string, out TimeSpan)"/>.
        /// Defaults to 5 minutes.
        /// </summary>
        public string MTPWorkItemTimeout { get; set; }

        /// <summary>
        /// Optional name of the TRX file produced by Microsoft.Testing.Extensions.TrxReport.
        /// Defaults to "testResults.trx". Whatever value is used here is what the arcade
        /// Python TRXFormat parser will pick up from the work item's working directory.
        /// </summary>
        public string TrxReportFilename { get; set; } = "testResults.trx";

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem.
        /// </summary>
        [Output]
        public ITaskItem[] MTPWorkItems { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            MTPWorkItems = (await Task.WhenAll(MTPProjects.Select(PrepareWorkItem))).Where(wi => wi != null).ToArray();
        }

        /// <summary>
        /// Prepares a HelixWorkItem for a single MTP test project.
        /// </summary>
        private async Task<ITaskItem> PrepareWorkItem(ITaskItem mtpProject)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            if (!mtpProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
            {
                return null;
            }
            if (!mtpProject.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
            {
                return null;
            }

            mtpProject.TryGetMetadata("Arguments", out string arguments);

            string assemblyName = Path.GetFileName(targetPath);
            string assemblyBaseName = assemblyName;
            if (assemblyBaseName.EndsWith(".dll"))
            {
                assemblyBaseName = assemblyBaseName.Substring(0, assemblyBaseName.Length - 4);
            }

            // MTP test apps are self-hosting executables. Run the assembly directly with
            // 'dotnet exec'. The reporter args below require the test project to reference
            // Microsoft.Testing.Extensions.TrxReport. MSTest.Sdk references it transitively;
            // xUnit v3 projects built with Microsoft.DotNet.Arcade.Sdk's XUnitV3 targets get
            // it implicitly as well. Other MTP-based frameworks must add the package.
            // The filename is wrapped in double quotes so spaces in user-provided values do
            // not split the argument. MTP itself rejects values containing path separators.
            // --auto-reporters off prevents any other MTP reporter extension the test project
            // happens to reference from auto-activating in the Helix work item (we want the
            // TRX reporter and nothing else, so the arcade Helix pipeline sees a single set
            // of results).
            string reporterArgs =
                $"--results-directory . --report-trx --report-trx-filename \"{TrxReportFilename}\" --auto-reporters off";

            string command = $"{PathToDotnet} exec --roll-forward Major " +
                $"--runtimeconfig {assemblyBaseName}.runtimeconfig.json " +
                $"--depsfile {assemblyBaseName}.deps.json " +
                $"{assemblyName} {reporterArgs}" +
                (string.IsNullOrEmpty(arguments) ? "" : " " + arguments);

            Log.LogMessage($"Creating MTP work item with properties Identity: {assemblyName}, PayloadDirectory: {publishDirectory}, Command: {command}");

            TimeSpan timeout = TimeSpan.FromMinutes(5);
            if (!string.IsNullOrEmpty(MTPWorkItemTimeout) && !TimeSpan.TryParse(MTPWorkItemTimeout, out timeout))
            {
                Log.LogWarning($"Invalid value \"{MTPWorkItemTimeout}\" provided for MTPWorkItemTimeout; falling back to default value of \"00:05:00\" (5 minutes)");
                timeout = TimeSpan.FromMinutes(5);
            }

            var result = new Microsoft.Build.Utilities.TaskItem(assemblyName, new Dictionary<string, string>()
            {
                { "Identity", assemblyName },
                { "PayloadDirectory", publishDirectory },
                { "Command", command },
                { "Timeout", timeout.ToString() },
            });
            mtpProject.CopyMetadataTo(result);
            return result;
        }
    }
}
