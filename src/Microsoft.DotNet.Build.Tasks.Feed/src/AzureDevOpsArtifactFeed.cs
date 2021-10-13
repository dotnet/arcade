// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// Represents the body of a request sent when creating a new feed.
    /// </summary>
    /// <remarks>>
    /// When creating a new feed, we want to set up permissions based on the org and project.
    /// Right now, only dnceng's public and internal projects are supported.
    /// New feeds automatically get the feed administrators and project collection administrators as owners,
    /// but we want to automatically add some additional permissions so that the build services can push to them,
    /// and aadTenant users can read from them.
    /// </remarks>
    public class AzureDevOpsArtifactFeed
    {
        public AzureDevOpsArtifactFeed(string name, string organization, string project)
        {
            Name = name;
            switch (organization)
            {
                case "dnceng":
                    switch (project)
                    {
                        case "public":
                        case "internal":
                            Permissions = new List<AzureDevOpsFeedPermission>
                            {
                                // Project Collection Build Service
                                new AzureDevOpsFeedPermission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:7ea9116e-9fac-403d-b258-b31fcf1bb293", "contributor"),
                                // internal Build Service
                                new AzureDevOpsFeedPermission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:b55de4ed-4b5a-4215-a8e4-0a0a5f71e7d8", "contributor"),
                                // Project administrators
                                new AzureDevOpsFeedPermission("Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1349140002-2196814402-2899064621-3782482097-0-0-0-0-1", "administrator"),
                            };
                            break;
                        default:
                            throw new NotImplementedException($"Project '{project}' within organization '{organization}' contains no feed permissions information.");
                    }
                    break;
                default:
                    throw new NotImplementedException($"Organization '{organization}' contains no feed permissions information.");

            }
        }

        public string Name { get; set; }

        public List<AzureDevOpsFeedPermission> Permissions { get; private set; }
    }
}
