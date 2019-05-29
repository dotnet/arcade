// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.Github.IssueLabeler;
using System;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args[0].Equals("Download", StringComparison.CurrentCultureIgnoreCase))
            {
                CommandLineOptions myOptions = null;
                Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(opts => myOptions = opts);

                if (myOptions != null)
                {
                    GithubIssueDownloader githubIssueDownloader = new GithubIssueDownloader(myOptions.GithubToken,
                                                                                            myOptions.Repository,
                                                                                            myOptions.Owner,
                                                                                            myOptions.StartIndex,
                                                                                            myOptions.EndIndex,
                                                                                            myOptions.Output);
                    githubIssueDownloader.DownloadAndSaveAsync().Wait();
                }
                return;
            }
            else
            {
                BuildWebHost(args).Run();
            }
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseApplicationInsights()
                .Build();
        }
    }
}
