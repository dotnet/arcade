// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using Azure.Core;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Microsoft.DotNet.Internal.SymbolHelper;

public sealed class SymbolPublisherOptions
{
    public SymbolPublisherOptions(
        string tenant,
        TokenCredential credential,
        IEnumerable<string>? packageFileExcludeList = null,
        bool convertPortablePdbs = false,
        bool treatPdbConversionIssuesAsInfo = false,
        IEnumerable<int>? pdbConversionTreatAsWarning = null,
        bool dotnetInternalPublishSpecialClrFiles = false,
        bool verboseClient = false,
        bool isDryRun = false)
    {
        Tenant = tenant is not null and not "" ? tenant : throw new ArgumentException("Tenant can't be null or empty", nameof(tenant));
        Credential = credential ?? throw new ArgumentNullException(nameof(credential)) ?? throw new ArgumentNullException(nameof(credential));
        PackageFileExcludeList = packageFileExcludeList is null ? FrozenSet<string>.Empty : packageFileExcludeList.ToFrozenSet();
        ConvertPortablePdbs = convertPortablePdbs;
        TreatPdbConversionIssuesAsInfo = treatPdbConversionIssuesAsInfo;
        PdbConversionTreatAsWarning = pdbConversionTreatAsWarning is null ? FrozenSet<int>.Empty : pdbConversionTreatAsWarning.ToFrozenSet();
        DotnetInternalPublishSpecialClrFiles = dotnetInternalPublishSpecialClrFiles;
        VerboseClient = verboseClient;
        IsDryRun = isDryRun;
    }

    public string Tenant { get; }
    public TokenCredential Credential { get; }
    public FrozenSet<string> PackageFileExcludeList { get; }
    public bool ConvertPortablePdbs { get; }
    public bool TreatPdbConversionIssuesAsInfo { get; }
    public FrozenSet<int> PdbConversionTreatAsWarning { get; }
    public bool DotnetInternalPublishSpecialClrFiles { get; }
    public bool VerboseClient { get; }
    public bool IsDryRun { get; }
}
