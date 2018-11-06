using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.Sdk
{
    class CreateXUnitWorkItems : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string[] XUnitProjects { get; set; }

        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            await Task.WhenAll(XUnitProjects.Select(PrepareWorkItem));

            return;
        }

        private async Task PrepareWorkItem(string project)
        {
            await Task.Yield();
        }
    }
}
