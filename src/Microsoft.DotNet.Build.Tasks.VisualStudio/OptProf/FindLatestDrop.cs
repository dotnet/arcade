using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VisualStudio
{
    /// <summary>
    /// Find the latest drop in a JSON list of VS drops.
    /// </summary>
    public sealed class FindLatestDrop : Task
    {
        /// <summary>
        /// Full path to JSON file containing list of drops.
        /// </summary>
        [Required]
        public string DropListPath { get; set; }

        /// <summary>
        /// The name of the latest drop.
        /// </summary>
        [Output]
        public string DropName { get; private set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            try
            {
                DropName = GetLatestDropName(File.ReadAllText(DropListPath));
            }
            catch (Exception e)
            {
                Log.LogError($"Error parsing file '{DropListPath}': {e.Message}");
                return;
            }
        }

        internal static string GetLatestDropName(string json)
        {
            JObject candidate = null;
            DateTime latest = default;
            foreach (JObject item in JArray.Parse(json))
            {
                var dt = DateTime.Parse((string)item["CreatedDateUtc"]);
                if (candidate == null || (bool)candidate["UploadComplete"] && !(bool)candidate["DeletePending"] && dt > latest)
                {
                    candidate = item;
                    latest = dt;
                }
            }

            return (string)candidate["Name"];
        }
    }
}
