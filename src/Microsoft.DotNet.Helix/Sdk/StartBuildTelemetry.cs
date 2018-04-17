using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class StartBuildTelemetry : HelixTask
    {
        /// <summary>
        /// The Job Source
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// The Job Type
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// The Build
        /// </summary>
        public string Build { get; set; }
        /// <summary>
        /// The QueueId
        /// </summary>
        public string QueueId { get; set; }
        /// <summary>
        /// The Attempt
        /// </summary>
        public string Attempt { get; set; }
        /// <summary>
        /// The Build Uri
        /// </summary>
        public string BuildUri { get; set; }
        /// <summary>
        /// Additional Properties to add to the build
        /// </summary>
        public string[] Properties { get; set; }

        protected override async Task<bool> ExecuteCore()
        {
            var propertiesDict = new Dictionary<string, string>();
            if (Properties != null)
            {
                foreach (string prop in Properties)
                {
                    string[] parts = prop.Split('=');
                    if (parts.Length != 2)
                    {
                        Log.LogError($"The property '{prop}' is in an invalid format.");
                        return false;
                    }
                    propertiesDict.Add(parts[0], parts[1]);
                }
            }

            var info = new JobInfo
            {
                Source = Source,
                Type = Type,
                Build = Build,
                QueueId = QueueId,
                Properties = propertiesDict,
                Attempt = Attempt,
                InitialWorkItemCount = 1,
            };

            Log.LogMessage(MessageImportance.Normal, $"Created JobInfo: '{JsonConvert.SerializeObject(info, Constants.SerializerSettings)}'");

            Log.LogMessage(MessageImportance.Normal, "Sending Job Start to helix api");
            string token = await HelixApi.Telemetry.StartJobAsync(info);


            Log.LogMessage(MessageImportance.Normal, "Sending Build Start to helix api");
            string workItemId = await HelixApi.Telemetry.StartBuildWorkItemAsync(token, BuildUri);


            Log.LogMessage(MessageImportance.Normal, "Saving information to config file");

            var config = new HelixConfig
            {
                JobToken = token,
                WorkItemId = workItemId,
            };

            return SetHelixConfig(config);
        }
    }
}
