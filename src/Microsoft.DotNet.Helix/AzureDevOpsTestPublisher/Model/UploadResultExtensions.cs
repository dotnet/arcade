// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;

public static class UploadResultExtensions
{
    public static UploadResult Aggregate(this UploadResult value, UploadResult other)
    {
        return (UploadResult)Math.Max((int)value, (int)other);
    }
}
