// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.SignTool
{
    public class SignToolTask : Task
    {
        /// <summary>
        /// Perform validation but do not actually send signing request to the server.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// True to test sign, otherwise real sign.
        /// </summary>
        public bool TestSign { get; set; }

        /// <summary>
        ///  Path to SignToolData.json.
        /// </summary>
        [Required]
        public string ConfigFilePath { get; set; }

        /// <summary>
        /// Directory containing binaries produced by the build.
        /// </summary>
        [Required]
        public string OutputDir { get; set; }

        /// <summary>
        /// Working directory used for storing files created during signing.
        /// </summary>
        [Required]
        public string TempDir { get; set; }

        /// <summary>
        /// Path to MicroBuild.Core package directory.
        /// </summary>
        [Required]
        public string MicroBuildCorePath { get; set; }

        /// <summary>
        /// Path to msbuild.exe. Required if <see cref="DryRun"/> is <c>false</c>.
        /// </summary>
        public string MSBuildPath { get; set; }

        /// <summary>
        /// Directory to write log to. Required if <see cref="DryRun"/> is <c>false</c>.
        /// </summary>
        public string LogDir { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            if (!DryRun && typeof(object).Assembly.GetName().Name != "mscorlib")
            {
                if (!File.Exists(MSBuildPath))
                {
                    Log.LogError($"File '{MSBuildPath}' not found.");
                    return;
                }
            }

            var signToolArgs = new SignToolArgs(
                outputPath: OutputDir,
                tempPath: TempDir,
                microBuildCorePath: MicroBuildCorePath,
                testSign: TestSign);

            var signTool = DryRun ? new ValidationOnlySignTool(signToolArgs) : (SignTool)new RealSignTool(signToolArgs, MSBuildPath, LogDir);
            var batchData = Configuration.ReadConfigFile(signToolArgs.OutputDir, ConfigFilePath, Log);
            var util = new BatchSignUtil(BuildEngine, Log, signTool, batchData, null);

            util.Go();
        }
    }
}
