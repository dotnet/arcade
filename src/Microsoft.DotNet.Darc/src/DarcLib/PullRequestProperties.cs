// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class PullRequestProperties
    {
        public const string TitleTag = "[Darc-Update]";

        public const string Description =
            "Darc is trying to update these files to the latest versions found in the Product Dependency Store";

        public const string AutoMergeTitle = "Auto-merging PR by Darc";
        public const string AutoMergeMessage = "Darc was instructed to merge this PR";
        public static readonly string Title = $"{TitleTag} global.json, Version.props and Version.Details.xml";
    }
}
