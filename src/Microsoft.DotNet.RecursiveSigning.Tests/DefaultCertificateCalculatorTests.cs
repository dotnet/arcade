// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public class DefaultCertificateCalculatorTests
    {
        [Fact]
        public void CalculateCertificateIdentifier_FileNameMatch_WinsOverExtension()
        {
            var rules = new DefaultCertificateRules(
                certificatesByFriendlyName: new Dictionary<string, JsonElement>
                {
                    ["DemoCertA"] = JsonDocument.Parse("""{"friendlyName":"DemoCertA"}""").RootElement.Clone(),
                    ["DemoCertB"] = JsonDocument.Parse("""{"friendlyName":"DemoCertB"}""").RootElement.Clone(),
                },
                fileNameMappings: new Dictionary<string, string>
                {
                    ["special.dll"] = "DemoCertA"
                },
                fileExtensionMappings: new Dictionary<string, string>
                {
                    [".dll"] = "DemoCertB"
                });
            var calculator = new DefaultCertificateCalculator(rules);
            var configuration = new SigningConfiguration("temp");

            var certificate = calculator.CalculateCertificateIdentifier(new FileMetadata("special.dll"), configuration);

            certificate.Should().NotBeNull();
            certificate!.Name.Should().Be("DemoCertA");
        }

        [Fact]
        public void CalculateCertificateIdentifier_ExtensionMatch_UsedAsFallback()
        {
            var rules = new DefaultCertificateRules(
                certificatesByFriendlyName: new Dictionary<string, JsonElement>
                {
                    ["DemoCertC"] = JsonDocument.Parse("""{"friendlyName":"DemoCertC"}""").RootElement.Clone()
                },
                fileNameMappings: new Dictionary<string, string>(),
                fileExtensionMappings: new Dictionary<string, string>
                {
                    [".exe"] = "DemoCertC"
                });
            var calculator = new DefaultCertificateCalculator(rules);
            var configuration = new SigningConfiguration("temp");

            var certificate = calculator.CalculateCertificateIdentifier(new FileMetadata("app.exe"), configuration);

            certificate.Should().NotBeNull();
            certificate!.Name.Should().Be("DemoCertC");
        }

        [Fact]
        public void CalculateCertificateIdentifier_NoMatches_ReturnsNull()
        {
            var rules = new DefaultCertificateRules(
                certificatesByFriendlyName: new Dictionary<string, JsonElement>
                {
                    ["DemoCertB"] = JsonDocument.Parse("""{"friendlyName":"DemoCertB"}""").RootElement.Clone()
                },
                fileNameMappings: new Dictionary<string, string>(),
                fileExtensionMappings: new Dictionary<string, string>
                {
                    [".dll"] = "DemoCertB"
                });
            var calculator = new DefaultCertificateCalculator(rules);
            var configuration = new SigningConfiguration("temp");

            var certificate = calculator.CalculateCertificateIdentifier(new FileMetadata("readme.txt"), configuration);

            certificate.Should().BeNull();
        }

        [Fact]
        public void CalculateCertificateIdentifier_MissingCertificateDefinition_Throws()
        {
            var rules = new DefaultCertificateRules(
                certificatesByFriendlyName: new Dictionary<string, JsonElement>(),
                fileNameMappings: new Dictionary<string, string>
                {
                    ["special.dll"] = "MissingCert"
                },
                fileExtensionMappings: new Dictionary<string, string>());
            var calculator = new DefaultCertificateCalculator(rules);
            var configuration = new SigningConfiguration("temp");

            var act = () => calculator.CalculateCertificateIdentifier(new FileMetadata("special.dll"), configuration);

            act.Should().Throw<InvalidOperationException>();
        }
    }
}


