// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Default certificate rule set used by DefaultCertificateCalculator.
    /// </summary>
    public sealed class DefaultCertificateRules
    {
        public IReadOnlyDictionary<string, JsonElement> CertificatesByFriendlyName { get; }
        public IReadOnlyDictionary<string, string> FileNameMappings { get; }
        public IReadOnlyDictionary<string, string> FileExtensionMappings { get; }

        public DefaultCertificateRules(
            IReadOnlyDictionary<string, JsonElement>? certificatesByFriendlyName,
            IReadOnlyDictionary<string, string>? fileNameMappings,
            IReadOnlyDictionary<string, string>? fileExtensionMappings)
        {
            var normalizedCertificates = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (certificatesByFriendlyName != null)
            {
                foreach (var (friendlyName, certificateDefinition) in certificatesByFriendlyName)
                {
                    if (string.IsNullOrWhiteSpace(friendlyName))
                    {
                        throw new ArgumentException("Certificate friendly names must not be null or whitespace.", nameof(certificatesByFriendlyName));
                    }

                    normalizedCertificates[friendlyName.Trim()] = certificateDefinition.Clone();
                }
            }

            CertificatesByFriendlyName = normalizedCertificates;
            FileNameMappings = NormalizeMappings(fileNameMappings, normalizeExtensionKeys: false);
            FileExtensionMappings = NormalizeMappings(fileExtensionMappings, normalizeExtensionKeys: true);
        }

        public bool TryGetCertificateDefinition(string friendlyName, out JsonElement certificateDefinition)
        {
            return CertificatesByFriendlyName.TryGetValue(friendlyName, out certificateDefinition);
        }

        private static IReadOnlyDictionary<string, string> NormalizeMappings(
            IReadOnlyDictionary<string, string>? mappings,
            bool normalizeExtensionKeys)
        {
            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (mappings == null)
            {
                return normalized;
            }

            foreach (var (key, value) in mappings)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Mapping keys must not be null or whitespace.", nameof(mappings));
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Mapping values must not be null or whitespace.", nameof(mappings));
                }

                var normalizedKey = key.Trim();
                if (normalizeExtensionKeys && !normalizedKey.StartsWith(".", StringComparison.Ordinal))
                {
                    normalizedKey = "." + normalizedKey;
                }

                normalized[normalizedKey] = value.Trim();
            }

            return normalized;
        }
    }
}
