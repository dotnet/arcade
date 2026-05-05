// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using CommandLine;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    public sealed class JobMonitorOptions
    {
        // Helix API access token
        public string HelixAccessToken { get; set; }

        /// <summary>
        /// Azure DevOps build token
        /// </summary>
        public string SystemAccessToken { get; set; }

        public bool ShowHelp { get; private set; }

        [Option("organization", HelpText = "Organization name (e.g. 'dotnet' for 'dotnet/runtime').")]
        public string Organization { get; set; }

        [Option("repository", HelpText = "Repository name (e.g. 'runtime' for 'dotnet/runtime').")]
        public string RepositoryName { get; set; }

        [Option("pr-number", HelpText = "Pull request number for the build, if applicable.")]
        public int? PrNumber { get; set; }

        [Option("build-id", HelpText = "Azure DevOps build ID.")]
        public string BuildId { get; set; }

        [Option("collection-uri", HelpText = "Azure DevOps collection URI.")]
        public string CollectionUri { get; set; }

        [Option("team-project", HelpText = "Azure DevOps team project name.")]
        public string TeamProject { get; set; }

        [Option("helix-base-uri", HelpText = "Base URI for the Helix service.")]
        public string HelixBaseUri { get; set; } = "https://helix.dot.net/";

        [Option("polling-interval-seconds", HelpText = "Polling interval in seconds.", Default = 30)]
        public int PollingIntervalSeconds { get; set; } = 30;

        [Option("max-wait-minutes", HelpText = "Maximum run time in minutes.", Default = 360)]
        public int MaximumWaitMinutes { get; set; } = 360;

        [Option("job-monitor-name", HelpText = "Name of the Helix Job Monitor job in Azure DevOps.")]
        public string JobMonitorName { get; set; } = "HelixJobMonitor";

        [Option("working-directory", HelpText = "Directory used to stage downloaded test results.")]
        public string WorkingDirectory { get; set; }

        [Option("attempt", HelpText = "Azure DevOps attempt number for the current job.")]
        public int? Attempt { get; set; }

        [Option("stage-name", HelpText = "Name of the Azure DevOps pipeline stage the monitor is running in. Used to scope monitoring to that stage. Defaults to the SYSTEM_STAGENAME environment variable.")]
        public string StageName { get; set; }

        public static JobMonitorOptions Parse(string[] args)
        {
            JobMonitorOptions parsed = null;
            var parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = Console.Out;
            });

            parser.ParseArguments<JobMonitorOptions>(args)
                .WithParsed(options => parsed = options)
                .WithNotParsed(errors =>
                {
                    parsed = new JobMonitorOptions { ShowHelp = true };
                });

            if (parsed == null || parsed.ShowHelp)
            {
                return parsed ?? new JobMonitorOptions { ShowHelp = true };
            }

            parsed.ApplyEnvironmentDefaults();
            parsed.Validate();
            return parsed;
        }

        private void ApplyEnvironmentDefaults()
        {
            HelixAccessToken ??= Environment.GetEnvironmentVariable("HELIX_ACCESSTOKEN");
#if DEBUG
            SystemAccessToken ??= new Azure.Identity.DefaultAzureCredential(includeInteractiveCredentials: true)
                .GetToken(new Azure.Core.TokenRequestContext(["499b84ac-1321-427f-aa17-267ca6975798/.default"]))
                .Token;
#endif
            CollectionUri ??= Environment.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI");
            TeamProject ??= Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECT");
            BuildId ??= Environment.GetEnvironmentVariable("BUILD_BUILDID");
            SystemAccessToken ??= Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
            RepositoryName ??= Environment.GetEnvironmentVariable("BUILD_REPOSITORY_NAME");
            WorkingDirectory ??= System.IO.Path.Combine(System.IO.Path.GetTempPath(), "helix-job-monitor", BuildId ?? "unknown");
            PrNumber ??= GetEnvironmentInt("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER");
            Attempt ??= GetEnvironmentInt("SYSTEM_JOBATTEMPT");
            StageName ??= Environment.GetEnvironmentVariable("SYSTEM_STAGENAME");
        }

        private void Validate()
        {
            CollectionUri = EnsureTrailingSlash(RequireValue(CollectionUri, "collection-uri", "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"));
            TeamProject = RequireValue(TeamProject, "team-project", "SYSTEM_TEAMPROJECT");
            BuildId = RequireValue(BuildId, "build-id", "BUILD_BUILDID");
            SystemAccessToken = RequireValue(SystemAccessToken, "access-token", "SYSTEM_ACCESSTOKEN");

            if (string.IsNullOrWhiteSpace(RepositoryName))
            {
                throw new InvalidOperationException("A repository identifier must be provided either by argument or pipeline environment.");
            }

            if (string.IsNullOrWhiteSpace(Organization))
            {
                throw new InvalidOperationException("Organization must be provided either by argument or pipeline environment.");
            }

            if (string.IsNullOrWhiteSpace(StageName))
            {
                throw new InvalidOperationException("--stage-name (or the SYSTEM_STAGENAME environment variable) must be set.");
            }
        }

        private static string RequireValue(string value, string argumentName, string environmentName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required option --{argumentName} or environment variable {environmentName}.");
            }

            return value;
        }

        private static int? GetEnvironmentInt(string environmentName)
        {
            string value = Environment.GetEnvironmentVariable(environmentName);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string EnsureTrailingSlash(string uri)
            => uri.EndsWith('/') ? uri : uri + '/';
    }
}
