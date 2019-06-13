// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                if (!DateTime.TryParse((string)item["CreatedDateUtc"], out var timestamp))
                {
                    throw new ApplicationException($"Invalid timestamp: '{item["CreatedDateUtc"]}'");
                }

                if (string.IsNullOrEmpty((string)item["Name"]))
                {
                    throw new ApplicationException($"Drop has no name: {item}");
                }

                if (candidate == null || (bool)item["UploadComplete"] && !(bool)item["DeletePending"] && timestamp > latest)
                {
                    candidate = item;
                    latest = timestamp;
                }
            }

            if (candidate == null)
            {
                throw new ApplicationException($"No drop name found");
            }

            return (string)candidate["Name"];
        }
    }
}
