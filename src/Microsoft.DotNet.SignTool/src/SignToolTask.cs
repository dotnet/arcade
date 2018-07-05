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
        public bool testsign { get; set; }

        public string nugetPackagesPath { get; set; }
        public string orchestrationManifestPath { get; set; }
        public string configFilePath { get; set; }
        public string appBaseDirectory { get; set; }
        public string intermediateOutputPath { get; set; }
        public string msbuildBinaryLogFilePath { get; set; }
        public string msbuildPath { get; set; }
        public string outputPath { get; set; }

        public override bool Execute()
        {
            try
            {
                var signToolArgs = new SignToolArgs(
                    outputPath: outputPath,
                    msbuildPath: msbuildPath,
                    msbuildBinaryLogFilePath: msbuildBinaryLogFilePath,
                    intermediateOutputPath: intermediateOutputPath,
                    nugetPackagesPath: nugetPackagesPath,
                    appPath: appBaseDirectory,
                    configFile: configFilePath,
                    test: Test,
                    testSign: testsign,
                    orchestrationManifestPath: orchestrationManifestPath);

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
