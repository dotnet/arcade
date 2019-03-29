using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SDLAutomator
{
    public class InstallSDL : MSBuild.Task
    {
        [Required]
        public string PackagesDirectory { get; set; }

        [Required]
        public string SdlToolName { get; set; }

        [Required]
        public string SdlPackageVersion { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, "Inside InstallSDL");
            try
            {
                AddSdlToolsToPath();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
            return !Log.HasLoggedErrors;
        }

        public void AddSdlToolsToPath()
        {
            try
            {
                const string name = "PATH";
                string pathvar = System.Environment.GetEnvironmentVariable(name);
                var value = $"{pathvar};{PackagesDirectory}\\{SdlToolName}\\{SdlPackageVersion}\\tools\\;";
                Environment.SetEnvironmentVariable(name, value);
            }
            catch (System.Security.SecurityException se)
            {
                Log.LogError(se.Message, null);
            }
        }

    }
}
