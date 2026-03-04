// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Certificate calculator that resolves certificates by filename first, then extension.
    /// </summary>
    public sealed class DefaultCertificateCalculator : ICertificateCalculator
    {
        private readonly DefaultCertificateRules _rules;

        public DefaultCertificateCalculator(DefaultCertificateRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public ICertificateIdentifier? CalculateCertificateIdentifier(IFileMetadata metadata, SigningConfiguration configuration)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (TryResolveFriendlyName(metadata.FileName, out var friendlyName))
            {
                if (!_rules.TryGetCertificateDefinition(friendlyName, out var certificateDefinition))
                {
                    throw new InvalidOperationException($"Rule matched '{metadata.FileName}' to certificate '{friendlyName}', but that certificate is not defined.");
                }

                return new ESRPCertificateIdentifier(friendlyName, certificateDefinition);
            }

            return null;
        }

        private bool TryResolveFriendlyName(string fileName, out string friendlyName)
        {
            if (_rules.FileNameMappings.TryGetValue(fileName, out friendlyName!))
            {
                return true;
            }

            var extension = System.IO.Path.GetExtension(fileName);
            if (_rules.FileExtensionMappings.TryGetValue(extension, out friendlyName!))
            {
                return true;
            }

            friendlyName = string.Empty;
            return false;
        }
    }
}


