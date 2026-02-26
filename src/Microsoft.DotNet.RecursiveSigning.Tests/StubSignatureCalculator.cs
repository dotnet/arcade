// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

﻿#nullable enable

using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Stub signature calculator for Phase 1 testing.
    /// Always returns the same test certificate - no real certificate resolution.
    /// </summary>
    public sealed class StubSignatureCalculator : ISignatureCalculator
    {
        private const string TestCertificate = "TestCert";

        public ICertificateIdentifier? CalculateCertificateIdentifier(IFileMetadata metadata, SigningConfiguration configuration)
        {
            // For Phase 1, always return the same test certificate.
            // No PKT analysis, no target framework detection, no already-signed detection.
            return new TestCertificateIdentifier(TestCertificate);
        }

        private sealed class TestCertificateIdentifier : ICertificateIdentifier
        {
            public string Name { get; }

            public TestCertificateIdentifier(string name)
            {
                Name = name;
            }
        }
    }
}
