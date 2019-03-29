using System;
using Microsoft.Build.Framework;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SDLAutomator
{
    public class InstallGuardian : MSBuild.Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, "Inside InstallGuardian");
            try
            {
                
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
            return !Log.HasLoggedErrors;
        }

    }
}
