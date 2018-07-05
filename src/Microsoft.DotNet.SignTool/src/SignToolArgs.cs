using System;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct SignToolArgs
    {
        internal string OutputPath { get; }
        internal string IntermediateOutputPath { get; }
        internal string NuGetPackagesPath { get; }
        internal string AppPath { get; }
        internal string MSBuildPath { get; }
        internal string MSBuildBinaryLogFilePath { get; }
        internal string ConfigFile { get; }
        internal bool Test { get; }
        internal bool TestSign { get; }
        internal string OrchestrationManifestPath { get; }

        internal SignToolArgs(
            string outputPath,
            string intermediateOutputPath,
            string appPath,
            string msbuildPath,
            string msbuildBinaryLogFilePath,
            string nugetPackagesPath,
            string configFile,
            bool test,
            bool testSign,
            string orchestrationManifestPath
            )
        {
            OutputPath = outputPath;
            IntermediateOutputPath = intermediateOutputPath;
            AppPath = appPath;
            MSBuildPath = msbuildPath;
            MSBuildBinaryLogFilePath = msbuildBinaryLogFilePath;
            NuGetPackagesPath = nugetPackagesPath;
            ConfigFile = configFile;
            Test = test;
            TestSign = testSign;
            OrchestrationManifestPath = orchestrationManifestPath;

            if (String.IsNullOrEmpty(OutputPath))
            {
                throw new ArgumentException("Need a value for OutputPath.");
            }

            if (!test && String.IsNullOrEmpty(MSBuildPath))
            {
                throw new ArgumentException("msbuildpath argument must be specified unless running in validation-only (test) mode.");
            }
        }
    }
}
