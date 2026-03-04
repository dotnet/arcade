// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Text.Json;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Certificate identifier carrying an ESRP-specific signing operations definition.
    /// The <see cref="CertificateDefinition"/> is the raw JSON that drops into the
    /// <c>SigningInfo.Operations</c> field of the ESRP CLI submission.
    /// </summary>
    public sealed class ESRPCertificateIdentifier : ICertificateIdentifier
    {
        public string Name => FriendlyName;
        public string FriendlyName { get; }
        public JsonElement CertificateDefinition { get; }

        public ESRPCertificateIdentifier(string friendlyName, JsonElement certificateDefinition)
        {
            FriendlyName = friendlyName ?? throw new ArgumentNullException(nameof(friendlyName));
            CertificateDefinition = certificateDefinition.Clone();
        }
    }
}
