// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Validation.Tests
{
    public class CommonRepoResourcesFixture : IDisposable
    {
        public RepoResources CommonResources { get; private set; }

        public CommonRepoResourcesFixture()
        {
            CommonResources = RepoResources.Create(useIsolatedRoots: false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            CommonResources.Dispose();
        }
    }
}
