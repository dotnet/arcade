// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        public string OutputPath { get; set; }

        /// <summary>
        /// Working directory used for storing files created during signing.
        /// </summary>
        [Required]
        public string TempPath { get; set; }

        /// <summary>
        /// Path to MicroBuild.Core package directory.
        /// </summary>
        [Required]
        public string MicroBuildCorePath { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            try
            {
                var signToolArgs = new SignToolArgs(
                    outputPath: OutputPath,
                    tempPath: TempPath,
                    microBuildCorePath: MicroBuildCorePath,
                    testSign: TestSign);

                var signTool = DryRun ? new ValidationOnlySignTool(signToolArgs) : (SignTool)new RealSignTool(signToolArgs);
                var batchData = Configuration.ReadConfigFile(signToolArgs.OutputPath, ConfigFilePath, Log);
                var util = new BatchSignUtil(BuildEngine, Log, signTool, batchData, null);

                util.Go();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
            }
        }
    }
}
