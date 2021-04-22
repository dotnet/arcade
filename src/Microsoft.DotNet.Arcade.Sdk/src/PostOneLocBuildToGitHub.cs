// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Arcade.Sdk
{
    public class PostOneLocBuildToGitHub : MSBuildTaskBase
    {


        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<IGitHubClient>(new GitHubClient(new ProductHeaderValue("OneLocBuild")));
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton<IHelpers, Helpers>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(IGitHubClient gitHubClient, IFileSystem fileSystem, IHelpers helpers)
        {
            

            return true;
        }


    }
}
