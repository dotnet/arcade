using  Microsoft.Build.Utilities;
using System.Diagnostics;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class LaunchDebugger : Task
    {
        public override bool Execute()
        {
            Debugger.Launch();
            return true;
        }
    }
}
