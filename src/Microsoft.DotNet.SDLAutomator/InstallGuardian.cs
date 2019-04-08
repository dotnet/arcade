using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SDLAutomator
{
    public class InstallGuardian : MSBuild.Task
    {
        [Required]
        public string PackagesDirectory { get; set; }

        [Required]
        public string GuardianPackageVersion { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, "Inside InstallGuardian");
            try
            {
                AddGuardianToPath();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
            return !Log.HasLoggedErrors;
        }

        public void AddGuardianToPath()
        {
            try
            {
                const string name = "PATH";
                string pathvar = System.Environment.GetEnvironmentVariable(name);
                var value = $"{pathvar};{PackagesDirectory}\\microsoft.guardian.cli\\{GuardianPackageVersion}\\tools\\;";
                Environment.SetEnvironmentVariable(name, value);
            }
            catch (System.Security.SecurityException se)
            {
                Log.LogError(se.Message, null);
            }
        }

    }
}
