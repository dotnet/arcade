using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Helix.Sdk
{
    public static class TaskItemExtensions
    {
        public static bool TryGetMetadata(this ITaskItem item, string key, out string value)
        {
            value = item.GetMetadata(key);
            return !string.IsNullOrEmpty(value);
        }

        public static bool GetRequiredMetadata(this ITaskItem item, TaskLoggingHelper log, string key, out string value)
        {
            value = item.GetMetadata(key);
            if (string.IsNullOrEmpty(value))
            {
                log.LogError($"Item '{item.ItemSpec}' missing required metadata '{key}'.");
                return false;
            }

            return true;
        }
    }
}
