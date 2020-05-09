// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PublishArtifactsInManifestV3 : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }
        public async Task ExecuteAsync()
        {
            await Task.CompletedTask;
        }
    }
}
