// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class AzureDevOpsArtifactFeedTests
    {
        [Theory]
        [InlineData("public")]
        [InlineData("internal")]
        public void DncengFeedPermissions(string project)
        {
            var feed = new AzureDevOpsArtifactFeed("test-feed", "dnceng", project);

            Assert.Collection(
                feed.Permissions,
                permission => AssertPermission(
                    permission,
                    "Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:7ea9116e-9fac-403d-b258-b31fcf1bb293",
                    "reader"),
                permission => AssertPermission(
                    permission,
                    "Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:b55de4ed-4b5a-4215-a8e4-0a0a5f71e7d8",
                    "reader"),
                permission => AssertPermission(
                    permission,
                    "Microsoft.VisualStudio.Services.Claims.AadServicePrincipal;72f988bf-86f1-41af-91ab-2d7cd011db47\\2e3264a8-9ab9-408e-a29f-4de38d20b852",
                    "contributor"),
                permission => AssertPermission(
                    permission,
                    "Microsoft.TeamFoundation.Identity;S-1-9-1551374245-3991166389-1514870082-2833517066-1601300440-0-0-0-0-3",
                    "reader"));
        }

        private static void AssertPermission(
            AzureDevOpsFeedPermission permission,
            string identityDescriptor,
            string role)
        {
            Assert.Equal(identityDescriptor, permission.IdentityDescriptor);
            Assert.Equal(role, permission.Role);
        }
    }
}
