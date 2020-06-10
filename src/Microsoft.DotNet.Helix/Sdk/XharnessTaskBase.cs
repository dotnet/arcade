using System;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided Android application packages.
    /// </summary>
    public abstract class XHarnessTaskBase : BaseTask
    {
        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        /// <summary>
        /// Optional timeout for all created workitems
        /// Defaults to 1200s
        /// </summary>
        public string WorkItemTimeout { get; set; }

        /// <summary>
        /// Extra arguments that will be passed to the iOS/Android/... app that is being run
        /// </summary>
        public string AppArguments { get; set; }

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[] WorkItems { get; set; }

        protected TimeSpan ParseTimeout()
        {
            TimeSpan timeout = TimeSpan.FromMinutes(15);
            if (!string.IsNullOrEmpty(WorkItemTimeout))
            {
                if (!TimeSpan.TryParse(WorkItemTimeout, out timeout))
                {
                    const int defaultTimeoutInMinutes = 15;
                    Log.LogWarning($"Invalid value \"{WorkItemTimeout}\" provided for XHarnessWorkItemTimeout; " +
                        $"falling back to default value of \"00:{defaultTimeoutInMinutes}:00\" ({defaultTimeoutInMinutes} minutes)");
                    timeout = TimeSpan.FromMinutes(defaultTimeoutInMinutes);
                }
            }

            return timeout;
        }
    }
}
