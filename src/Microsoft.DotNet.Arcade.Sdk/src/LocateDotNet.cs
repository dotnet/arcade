// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Arcade.Sdk
{
    [MSBuildMultiThreadableTask]
    public class LocateDotNet : Task, IMultiThreadableTask
    {
        private readonly record struct CacheKey(AbsolutePath GlobalJsonPath, DateTime LastWrite, string Paths);

        /// <summary>Injected by MSBuild so paths resolve against the project directory in multithreaded builds.</summary>
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        [Required]
        public string RepositoryRoot { get; set; }

        [Output]
        public string DotNetPath { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            var globalJsonPath = TaskEnvironment.GetAbsolutePath(Path.Combine(RepositoryRoot, "global.json"));

            var lastWrite = File.GetLastWriteTimeUtc(globalJsonPath);
            var paths = TaskEnvironment.GetEnvironmentVariable("PATH");

            // The read/write pair below is not atomic, so under multithreaded execution two threads
            // can both miss and both compute the value. That is benign here: the computation is pure
            // and deterministic for a given (global.json, timestamp, PATH), so the loser of the race
            // has its registration dropped and the winner's identical entry stands. The cache is an
            // optimization, not a lock.
            var cacheKey = new CacheKey(globalJsonPath, lastWrite, paths);
            if (BuildEngine4.GetRegisteredTaskObject(cacheKey, RegisteredTaskObjectLifetime.Build) is string cachedPath)
            {
                Log.LogMessage(MessageImportance.Low, $"Reused cached value.");
                DotNetPath = cachedPath;
                return;
            }

            var globalJson = File.ReadAllText(globalJsonPath);

            // avoid Newtonsoft.Json dependency
            var match = Regex.Match(globalJson, @"""dotnet""\s*:\s*""([^""]+)""");
            if (!match.Success)
            {
                Log.LogError($"Unable to determine dotnet version from file '{globalJsonPath}'.");
                return;
            }

            var sdkVersion = match.Groups[1].Value;

            var fileName = (Path.DirectorySeparatorChar == '\\') ? "dotnet.exe" : "dotnet";
            var dotNetDir = paths.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(p => File.Exists(TaskEnvironment.GetAbsolutePath(Path.Combine(p, fileName))));

            if (dotNetDir == null || !Directory.Exists(TaskEnvironment.GetAbsolutePath(Path.Combine(dotNetDir, "sdk", sdkVersion))))
            {
                Log.LogError($"Unable to find dotnet with SDK version '{sdkVersion}'");
                return;
            }

            DotNetPath = TaskEnvironment.GetAbsolutePath(Path.Combine(dotNetDir, fileName));
            BuildEngine4.RegisterTaskObject(cacheKey, DotNetPath, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: true);
        }
    }
}
