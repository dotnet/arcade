// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Reads certificate rules from JSON.
    /// </summary>
    public sealed class DefaultCertificateRulesReader
    {
        public DefaultCertificateRules ReadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
            }

            var json = File.ReadAllText(filePath);
            return ReadFromJson(json);
        }

        public DefaultCertificateRules ReadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON payload cannot be null or whitespace.", nameof(json));
            }

            var document = JsonSerializer.Deserialize<DefaultCertificateRulesDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidOperationException("Failed to deserialize certificate rules JSON.");

            var certificates = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var cert in document.Certificates ?? Array.Empty<JsonElement>())
            {
                if (!cert.TryGetProperty("friendlyName", out var friendlyNameElement))
                {
                    throw new InvalidOperationException("Each certificate must define friendlyName.");
                }

                var friendlyName = friendlyNameElement.GetString();
                if (string.IsNullOrWhiteSpace(friendlyName))
                {
                    throw new InvalidOperationException("Each certificate must define a non-empty friendlyName.");
                }

                certificates[friendlyName] = cert.Clone();
            }

            return new DefaultCertificateRules(
                certificates,
                document.Rules?.FileNameMappings,
                document.Rules?.FileExtensionMappings);
        }

        private sealed class DefaultCertificateRulesDocument
        {
            public JsonElement[]? Certificates { get; set; }
            public RuleMappingsDocument? Rules { get; set; }
        }

        private sealed class RuleMappingsDocument
        {
            public Dictionary<string, string>? FileNameMappings { get; set; }
            public Dictionary<string, string>? FileExtensionMappings { get; set; }
        }

    }
}

