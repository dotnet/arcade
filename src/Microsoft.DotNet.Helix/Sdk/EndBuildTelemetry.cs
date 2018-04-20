using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class EndBuildTelemetry : HelixTask
    {
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        protected override async Task<bool> ExecuteCore()
        {
            var config = GetHelixConfig();
            if (string.IsNullOrEmpty(config.JobToken) || string.IsNullOrEmpty(config.WorkItemId))
            {
                Log.LogError($"The file at '{ConfigFile}' does not have a JobToken or WorkItemId in it.");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, "Sending Build Finish Information");
            await HelixApi.Telemetry.FinishBuildWorkItemAsync(
                config.JobToken,
                config.WorkItemId,
                ErrorCount,
                WarningCount);

            return SetHelixConfig(new HelixConfig());
        }
    }
}
