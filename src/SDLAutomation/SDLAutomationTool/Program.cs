using System;
using System.IO;

namespace SDLAutomationTool
{
    class Program
    {
        static void Main(string[] args)
        {
            String workingDirectory = (args != null && args.Length > 0) ? args[0]: Directory.GetCurrentDirectory();             
            String guardianInstallPath = Path.Join(workingDirectory, "packages");
            String nugetInstallPath = Path.Join(guardianInstallPath, "Tools");

            GuardianPrep gp = new GuardianPrep();
            gp.InstallGuardian(guardianInstallPath);
            gp.AddGuardianToPATH(guardianInstallPath, nugetInstallPath);
            gp.InitGuardian(workingDirectory);
            gp.RunTool(@".\tsa.credscan.gdnconfig", workingDirectory+@"\.gdn\r\GuardianResultsSummary.tsv", workingDirectory);
            gp.FileBugs(@".\TSAOptions.json", workingDirectory);

        }
    }
}
