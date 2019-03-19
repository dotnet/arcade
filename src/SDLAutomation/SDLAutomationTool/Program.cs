using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace SDLAutomationTool
{
    class Program
    {
        private static void Error(string message)
        {
            Console.Error.WriteLine("fatal: " + message);
            Environment.Exit(-1);
        }

        private static void MissingArgument(string name)
        {
            Error($"Missing required argument {name}");
        }

        private static int Main(string[] args)
        {
            string logLevel = "standard";
            string baseline = "baseline";
            bool updateBaseline = false;
            string workingDirectory = null;
            string guardianInstallPath = null;
            bool showHelp = false;


            var options = new OptionSet
            {
                {"w|working-directory=", "The working directory to run SDL tools against", s => workingDirectory = s},
                {"g|guardian-install-path=", "The Path to install Guardian", n => guardianInstallPath = n},
                {"l|logger-level=", "The level of logging that is needed eg., standard", l => logLevel = l},
                {"b|baseline=", "The display name for all the baseline data", b => baseline = b},
                {"u|update-baseline=", "Would you like to update baseline data for every run or not? ", ub => updateBaseline = ub != null},
                {"h|?|help", "Display this help message.", h => showHelp = h != null},
            };

            List<string> arguments = options.Parse(args);

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            if (string.IsNullOrEmpty(workingDirectory))
            {
                MissingArgument(nameof(workingDirectory));
            }

            if (string.IsNullOrEmpty(guardianInstallPath))
            {
                MissingArgument(nameof(guardianInstallPath));
            }

            string nugetInstallPath = Path.Join(guardianInstallPath, "Tools");

            ILogger _log = new LoggerFactory().CreateLogger("dotnet-SDL");

            var gp = new GuardianPrep(_log);
            gp.InstallGuardian(guardianInstallPath);
            gp.AddGuardianToPath(guardianInstallPath, nugetInstallPath);
            gp.InitGuardian(workingDirectory, logLevel);
            gp.RunTool(@".\tsa.credscan.gdnconfig", workingDirectory + @"\.gdn\r\GuardianResultsSummary.tsv", workingDirectory, logLevel, baseline, updateBaseline);
            gp.FileBugs(@".\TSAOptions.json", workingDirectory, logLevel);

            return 0;
        }
    }
}
