// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Utilities;
using System;

namespace Microsoft.DotNet.SignTool
{
    public class SignToolTask : Task
    {
        private const bool ExitWithFailure = false;

        public bool Test { get; set; }
        public bool TestSign { get; set; }

        public string NuGetPackagesPath { get; set; }
        public string OrchestrationManifestPath { get; set; }
        public string ConfigFilePath { get; set; }
        public string AppBaseDirectory { get; set; }
        public string IntermediateOutputPath { get; set; }
        public string MSBuildBinaryLogFilePath { get; set; }
        public string MSBuildPath { get; set; }
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            try
            {
                var signToolArgs = new SignToolArgs(
                    outputPath: OutputPath,
                    msbuildPath: MSBuildPath,
                    msbuildBinaryLogFilePath: MSBuildBinaryLogFilePath,
                    intermediateOutputPath: IntermediateOutputPath,
                    nugetPackagesPath: NuGetPackagesPath,
                    appPath: AppBaseDirectory,
                    configFile: ConfigFilePath,
                    test: Test,
                    testSign: TestSign,
                    orchestrationManifestPath: OrchestrationManifestPath);

                BatchSignInput batchData;
                var signTool = SignToolFactory.Create(signToolArgs);
                string configFileKind = Program.GetConfigFileKind(signToolArgs.ConfigFile);

                switch (configFileKind.ToLower())
                {
                    case "default":
                        batchData = Program.ReadConfigFile(signToolArgs.OutputPath, signToolArgs.ConfigFile);
                        break;
                    case "orchestration":
                        batchData = Program.ReadOrchestrationConfigFile(signToolArgs.OutputPath, signToolArgs.ConfigFile);
                        break;
                    default:
                        Log.LogError($"signtool : error : Don't know how to deal with manifest kind '{configFileKind}'");
                        return ExitWithFailure;
                }

                var util = new BatchSignUtil(signTool, batchData, signToolArgs.OrchestrationManifestPath);

                return util.Go(Console.Out) ? !Log.HasLoggedErrors : ExitWithFailure;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return ExitWithFailure;
            }
        }
    }
}
