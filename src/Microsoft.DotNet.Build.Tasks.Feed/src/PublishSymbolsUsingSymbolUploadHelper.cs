// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Build.Framework;
using Microsoft.DotNet.ArcadeAzureIntegration;
using Microsoft.DotNet.Internal.SymbolHelper;
using MsBuildUtils = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed;

/// <summary>
/// MSBuild task that publishes symbols to a symbol server using the internal SymbolUploadHelper
/// infrastructure. Supports both PAT-based auth (legacy) and Entra/managed identity auth.
/// </summary>
public class PublishSymbolsUsingSymbolUploadHelper : MsBuildUtils.Task
{
    /// <summary>
    /// The Azure DevOps organization to publish symbols to (e.g., "microsoft" or "microsoftpublicsymbols").
    /// </summary>
    [Required]
    public string AzdoOrg { get; set; }

    /// <summary>
    /// Optional personal access token. If provided, PAT-based auth is used.
    /// If empty or not set, DefaultIdentityTokenCredential (Entra/MI) is used.
    /// </summary>
    public string PersonalAccessToken { get; set; }

    /// <summary>
    /// Optional managed identity client ID for DefaultIdentityTokenCredential.
    /// </summary>
    public string ManagedIdentityClientId { get; set; }

    /// <summary>
    /// Symbol packages (*.symbols.nupkg) to publish.
    /// </summary>
    public ITaskItem[] PackagesToPublish { get; set; }

    /// <summary>
    /// Individual files (PDBs, DLLs, etc.) to publish.
    /// </summary>
    public ITaskItem[] FilesToPublish { get; set; }

    /// <summary>
    /// Files to exclude from symbol packages during publishing.
    /// </summary>
    public ITaskItem[] PackageExcludeFiles { get; set; }

    /// <summary>
    /// Number of days before the symbol request expires. Default is 3650.
    /// </summary>
    public int ExpirationInDays { get; set; } = 3650;

    /// <summary>
    /// Whether to enable verbose logging from the symbol client.
    /// </summary>
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Whether to perform a dry run without actually publishing.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Whether to convert portable PDBs to Windows PDBs.
    /// </summary>
    public bool ConvertPortablePdbsToWindowsPdbs { get; set; }

    /// <summary>
    /// Whether to treat PDB conversion issues as informational messages.
    /// </summary>
    public bool TreatPdbConversionIssuesAsInfo { get; set; }

    /// <summary>
    /// Comma-separated list of PDB conversion diagnostic IDs to treat as warnings.
    /// </summary>
    public string PdbConversionTreatAsWarning { get; set; }

    /// <summary>
    /// Whether to publish special CLR files (DAC, DBI, SOS) under diagnostic indexes.
    /// </summary>
    public bool PublishSpecialClrFiles { get; set; }

    /// <summary>
    /// Directory containing loose PDB/DLL files to publish.
    /// If set, files from this directory are added via AddDirectory instead of individual file items.
    /// </summary>
    public string PDBArtifactsDirectory { get; set; }

    public override bool Execute()
    {
        return ExecuteAsync().GetAwaiter().GetResult();
    }

    private async Task<bool> ExecuteAsync()
    {
        try
        {
            TokenCredential credential = CreateCredential();
            TaskTracer tracer = new(Log, verbose: VerboseLogging);

            IEnumerable<int> pdbWarnings = ParsePdbConversionWarnings();
            FrozenSet<string> exclusions = PackageExcludeFiles?.Select(i => i.ItemSpec).ToFrozenSet()
                ?? FrozenSet<string>.Empty;

            SymbolPublisherOptions options = new(
                AzdoOrg,
                credential,
                packageFileExcludeList: exclusions,
                convertPortablePdbs: ConvertPortablePdbsToWindowsPdbs,
                treatPdbConversionIssuesAsInfo: TreatPdbConversionIssuesAsInfo,
                pdbConversionTreatAsWarning: pdbWarnings,
                dotnetInternalPublishSpecialClrFiles: PublishSpecialClrFiles,
                verboseClient: VerboseLogging,
                isDryRun: DryRun);

            SymbolUploadHelper helper = DryRun
                ? SymbolUploadHelperFactory.GetSymbolHelperFromLocalTool(tracer, options, ".")
                : await SymbolUploadHelperFactory.GetSymbolHelperWithDownloadAsync(tracer, options);

            string requestName = $"arcade-sdk/{AzdoOrg}/{Guid.NewGuid()}";
            Log.LogMessage(MessageImportance.High, "Creating symbol request '{0}' for org '{1}'", requestName, AzdoOrg);

            int result = await helper.CreateRequest(requestName);
            if (result != 0)
            {
                Log.LogError("Failed to create symbol request '{0}'. Exit code: {1}", requestName, result);
                return false;
            }

            bool succeeded = false;
            try
            {
                // Add loose files directory if specified
                if (!string.IsNullOrEmpty(PDBArtifactsDirectory))
                {
                    Log.LogMessage(MessageImportance.High, "Adding directory '{0}' to symbol request", PDBArtifactsDirectory);
                    result = await helper.AddDirectory(requestName, PDBArtifactsDirectory);
                    if (result != 0)
                    {
                        Log.LogError("Failed to add directory to symbol request. Exit code: {0}", result);
                        return false;
                    }
                }

                // Add individual file items if specified (and no directory was given)
                if (FilesToPublish?.Length > 0 && string.IsNullOrEmpty(PDBArtifactsDirectory))
                {
                    // SymbolUploadHelper works with directories, so we need to group files
                    // by their parent directory and add each directory.
                    var directories = FilesToPublish
                        .Select(f => System.IO.Path.GetDirectoryName(f.ItemSpec))
                        .Where(d => !string.IsNullOrEmpty(d))
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                    foreach (string dir in directories)
                    {
                        Log.LogMessage(MessageImportance.Normal, "Adding file directory '{0}' to symbol request", dir);
                        result = await helper.AddDirectory(requestName, dir);
                        if (result != 0)
                        {
                            Log.LogError("Failed to add directory '{0}' to symbol request. Exit code: {1}", dir, result);
                            return false;
                        }
                    }
                }

                // Add symbol packages
                if (PackagesToPublish?.Length > 0)
                {
                    IEnumerable<string> packagePaths = PackagesToPublish.Select(p => p.ItemSpec);
                    Log.LogMessage(MessageImportance.High, "Adding {0} symbol package(s) to request", PackagesToPublish.Length);

                    result = await helper.AddPackagesToRequest(requestName, packagePaths);
                    if (result != 0)
                    {
                        Log.LogError("Failed to add packages to symbol request. Exit code: {0}", result);
                        return false;
                    }
                }

                Log.LogMessage(MessageImportance.High, "Finalizing symbol request with expiration of {0} days", ExpirationInDays);
                result = await helper.FinalizeRequest(requestName, (uint)ExpirationInDays);
                if (result != 0)
                {
                    Log.LogError("Failed to finalize symbol request. Exit code: {0}", result);
                    return false;
                }

                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                {
                    Log.LogMessage(MessageImportance.High, "Symbol publishing failed. Deleting request '{0}'.", requestName);
                    await helper.DeleteRequest(requestName);
                }
            }

            Log.LogMessage(MessageImportance.High, "Successfully published symbols to '{0}'", AzdoOrg);
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private TokenCredential CreateCredential()
    {
        if (!string.IsNullOrEmpty(PersonalAccessToken))
        {
            Log.LogMessage(MessageImportance.Normal, "Using PAT-based authentication for symbol publishing");
            return new PATCredential(PersonalAccessToken);
        }

        Log.LogMessage(MessageImportance.Normal, "Using Entra/managed identity authentication for symbol publishing");
        var options = new DefaultIdentityTokenCredentialOptions();
        if (!string.IsNullOrEmpty(ManagedIdentityClientId))
        {
            options.ManagedIdentityClientId = ManagedIdentityClientId;
        }
        return new DefaultIdentityTokenCredential(options);
    }

    private IEnumerable<int> ParsePdbConversionWarnings()
    {
        if (string.IsNullOrEmpty(PdbConversionTreatAsWarning))
        {
            return [];
        }

        return PdbConversionTreatAsWarning
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse);
    }
}
