using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SDLAutomationTool
{
    public class GuardianPrep
    {
        private readonly ILogger _log;

        public GuardianPrep(ILogger log)
        {
            _log = log;
        }
        /// <summary>
        /// Download from a secure Nuget feed and Install Guardian to packagesDirectory, if Guardian is not already installed. 
        /// </summary>
        /// <param name="packagesDirectory"></param>
        public void InstallGuardian(string packagesDirectory)
        {
            string installerPath = @".\Tools\guardian-installer.exe";
            var startInfo = new ProcessStartInfo("cmd.exe", "/C " + installerPath + " " + packagesDirectory + " --non-interactive ");

            var process = new Process()
            {
                StartInfo = startInfo
            };

            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// Initialize Guardian once per repo at the workingDirectory.
        /// </summary>
        /// <param name="workingDirectory"></param>
        /// <param name="logLevel"></param>
        public void InitGuardian(string workingDirectory, string logLevel)
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/C guardian init --working-directory " + workingDirectory + " --logger-level " + logLevel);

            var process = new Process()
            {
                StartInfo = startInfo
            };

            process.Start();
            process.WaitForExit();
        }


        /// <summary>
        /// Run SDL Tool(s). 
        /// runConfigPath - Path to find the run config file which specifies the tools to be run and the parameters to run with. 
        /// outputResultPath - Path where the output file with the breaking results are stored 
        /// workingDirectory - repo
        /// logLevel - logger level for eg., standard
        /// baseline - Name for the baseline data
        /// updateBaseline - whether to update baseline for each run or not
        /// </summary>
        /// <param name="runConfigPath"></param>
        /// <param name="outputResultPath"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="logLevel"></param>
        /// <param name="baseline"></param>
        /// <param name="updateBaseline"></param>
        public void RunTool(string runConfigPath, string outputResultPath, string workingDirectory, string logLevel, string baseline, bool updateBaseline)
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/C guardian run --config " + runConfigPath + " --export-breaking-results-to-file " + outputResultPath + " --logger-level " + logLevel + " --baseline " + baseline + " --update-baseline "+ updateBaseline +" --working-directory " + workingDirectory);

            var process = new Process()
            {
                StartInfo = startInfo
            };

            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// Publish results of the SDL runs to TSA which involves creating bugs for failures identified in the account and project specified in TSA Options file.
        /// TSAConfigPath - TSA options file (TSAOptions.json) which specifies the account and project to file bugs at under specified AreaPath and IterationPath. 
        /// workingDirectory - repo
        /// logLevel - logger level for eg., standard
        /// </summary>
        /// <param name="TSAConfigPath"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="logLevel"></param>
        public void FileBugs(string TSAConfigPath, string workingDirectory, string logLevel)
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/C guardian tsa-publish --all-tools --config " + TSAConfigPath + " --working-directory " + workingDirectory + " --logger-level "+ logLevel);

            var process = new Process()
            {
                StartInfo = startInfo
            };
            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// Guardian and Nuget install path needs to be added to PATH environment variable.
        /// packagePath - Path where Guardian is installed.
        /// nugetPath - Path where nuget.exe exists.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="nugetPath"></param>
        public void AddGuardianToPath(string packagePath, string nugetPath)
        {
            try
            {
                const string name = "PATH";
                string pathvar = System.Environment.GetEnvironmentVariable(name);
                var value = pathvar + ";" + packagePath + @"\versions\Microsoft.Guardian.Cli.0.0.38\tools\\;" + nugetPath + ";";
                Environment.SetEnvironmentVariable(name, value);
            }
            catch (System.Security.SecurityException se)
            {
                _log.LogError(se.Message, null);
            }
        }
    }
}
