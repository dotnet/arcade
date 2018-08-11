// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class DarcSettings
    {
        public DarcSettings()
        {
        }

        public DarcSettings(GitRepoType gitType, string personalAccessToken)
        {
            GitType = gitType;
            PersonalAccessToken = personalAccessToken;
        }

        public string BuildAssetRegistryPassword { get; set; }

        public string PersonalAccessToken { get; set; }

        public string BuildAssetRegistryBaseUri { get; set; }

        public GitRepoType GitType { get; set; }
    }
}
