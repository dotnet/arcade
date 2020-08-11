// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.SharedFramework.Sdk
{
    public abstract partial class BuildTask : ITask
    {
        private TaskLoggingHelper _log = null;

        internal TaskLoggingHelper Log => _log ??= new TaskLoggingHelper(this);

        public IBuildEngine BuildEngine { get; set; }

        public ITaskHost HostObject { get; set; }

        public abstract bool ExecuteCore();

        public bool Execute()
        {
#if NET472
            AssemblyResolution.Log = Log;
#endif
            try
            {
                return ExecuteCore();
            }
            finally
            {
#if NET472
                AssemblyResolution.Log = null;
#endif
            }
        }
    }
}
