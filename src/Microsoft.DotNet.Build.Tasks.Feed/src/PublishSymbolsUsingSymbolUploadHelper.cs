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
/// MSBuild task that publishes symbols to symbol servers using the internal SymbolUploadHelper
/// infrastructure. Symbols are first staged in a temporary Azure DevOps org (default: "dnceng"),
/// then promoted to the target symbol servers (internal/public) via the SymbolRequest service.
/// Supports both PAT-based auth (legacy) and Entra/managed identity auth.
///
/// Note: Unlike the old PublishSymbols task (Microsoft.SymbolUploader.Build.Task), this task
/// does not validate that individual files are indexable during dry runs. The 1ES symbol
/// publishing tool now manages that validation. Dry runs will still decompose packages
/// and exercise the request lifecycle but skip per-file indexability checks.
/// </summary>
public class PublishSymbolsUsingSymbolUploadHelper : MsBuildUtils.Task
{
    /// <summary>
    /// The temporary/staging Azure DevOps organization where symbols are uploaded before promotion.
    /// Default is "dnceng". Symbols cannot be published directly to the target orgs
    /// (microsoftpublicsymbols/microsoft); they must be staged here first.
    /// </summary>
    public string StagingAzdoOrg { get; set; } = "dnceng";

    /// <summary>
    /// The project name used for the SymbolRequest promotion service registration.
    /// Required for non-dry-run publishing.
    /// </summary>
    public string SymbolRequestProject { get; set; }

    /// <summary>
    /// Optional personal access token for staging org auth. If provided, PAT-based auth is used.
    /// If empty or not set, DefaultIdentityTokenCredential (Entra/MI) is used.
    /// </summary>
    public string PersonalAccessToken { get; set; }

    /// <summary>
    /// Optional managed identity client ID for DefaultIdentityTokenCredential.
    /// Used for both staging upload and promotion service auth.
    /// </summary>
    public string ManagedIdentityClientId { get; set; }

    /// <summary>
    /// Whether to publish symbols to the internal symbol server (SymWeb/microsoft org).
    /// Default is true.
    /// </summary>
    public bool PublishToInternalServer { get; set; } = true;

    /// <summary>
    /// Whether to publish symbols to the public symbol server (MSDL/microsoftpublicsymbols org).
    /// Default is true.
    /// </summary>
    public bool PublishToPublicServer { get; set; } = true;

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
            if (!PublishToInternalServer && !PublishToPublicServer)
            {
                Log.LogMessage(MessageImportance.High, "Neither internal nor public symbol server publishing is requested. Skipping.");
                return true;
            }

            TokenCredential stagingCredential = CreateStagingCredential();
            TokenCredential promotionCredential = CreatePromotionCredential();
            TaskTracer tracer = new(Log, verbose: VerboseLogging);

            IEnumerable<int> pdbWarnings = ParsePdbConversionWarnings();
            FrozenSet<string> exclusions = PackageExcludeFiles?.Select(i => i.ItemSpec).ToFrozenSet()
                ?? FrozenSet<string>.Empty;

            SymbolPublisherOptions options = new(
                StagingAzdoOrg,
                stagingCredential,
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

            string requestName = $"arcade-sdk/{StagingAzdoOrg}/{Guid.NewGuid()}";
            Log.LogMessage(MessageImportance.High, "Creating symbol request '{0}' in staging org '{1}'", requestName, StagingAzdoOrg);

            int result = await helper.CreateRequest(requestName);
            if (result != 0)
            {
                Log.LogError("Failed to create symbol request '{0}'. Exit code: {1}", requestName, result);
                return false;
            }

            bool uploadSucceeded = false;
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

                Log.LogMessage(MessageImportance.High, "Finalizing symbol request in staging org with expiration of {0} days", ExpirationInDays);
                result = await helper.FinalizeRequest(requestName, (uint)ExpirationInDays);
                if (result != 0)
                {
                    Log.LogError("Failed to finalize symbol request. Exit code: {0}", result);
                    return false;
                }

                uploadSucceeded = true;
            }
            finally
            {
                if (!uploadSucceeded)
                {
                    Log.LogMessage(MessageImportance.High, "Symbol upload failed. Deleting request '{0}'.", requestName);
                    await helper.DeleteRequest(requestName);
                }
            }

            // Promote the finalized request to the target symbol servers
            SymbolPromotionHelper.Visibility visibility = PublishToPublicServer
                ? SymbolPromotionHelper.Visibility.Public
                : SymbolPromotionHelper.Visibility.Internal;

            if (DryRun)
            {
                Log.LogMessage(MessageImportance.High,
                    "Dry run: would register request '{0}' to project '{1}' with visibility '{2}' for {3} days.",
                    requestName, SymbolRequestProject, visibility, ExpirationInDays);
            }
            else
            {
                if (string.IsNullOrEmpty(SymbolRequestProject))
                {
                    Log.LogError("SymbolRequestProject is required for non-dry-run symbol publishing promotion.");
                    return false;
                }

                Log.LogMessage(MessageImportance.High,
                    "Promoting symbol request '{0}' to project '{1}' with visibility '{2}'",
                    requestName, SymbolRequestProject, visibility);

                if (!await SymbolPromotionHelper.RegisterAndPublishRequest(
                    tracer, promotionCredential, SymbolPromotionHelper.Environment.Prod,
                    SymbolRequestProject, requestName, (uint)ExpirationInDays, visibility))
                {
                    Log.LogError("Failed to register and promote symbol request to the target symbol servers.");
                    return false;
                }
            }

            Log.LogMessage(MessageImportance.High, "Successfully published and promoted symbols (visibility: {0})", visibility);
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private TokenCredential CreateStagingCredential()
    {
        if (!string.IsNullOrEmpty(PersonalAccessToken))
        {
            Log.LogMessage(MessageImportance.Normal, "Using PAT-based authentication for staging symbol upload");
            return new PATCredential(PersonalAccessToken);
        }

        Log.LogMessage(MessageImportance.Normal, "Using Entra/managed identity authentication for staging symbol upload");
        return CreateDefaultCredential();
    }

    private TokenCredential CreatePromotionCredential()
    {
        Log.LogMessage(MessageImportance.Normal, "Using Entra/managed identity authentication for symbol promotion");
        return CreateDefaultCredential();
    }

    private DefaultIdentityTokenCredential CreateDefaultCredential()
    {
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
