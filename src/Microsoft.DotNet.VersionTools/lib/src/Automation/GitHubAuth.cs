﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class GitHubAuth
    {
        public string AuthToken { get; }
        public string User { get; }
        public string Email { get; }

        public GitHubAuth(
            string authToken,
            string user = null,
            string email = null)
        {
            if (authToken == null)
            {
                throw new ArgumentNullException(nameof(authToken));
            }
            AuthToken = authToken;
            User = user ?? "dotnet-bot";
            Email = email ?? "dotnet-bot@microsoft.com";
        }
    }
}
