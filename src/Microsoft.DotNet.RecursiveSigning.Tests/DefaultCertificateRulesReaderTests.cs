// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using FluentAssertions;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public class DefaultCertificateRulesReaderTests
    {
        [Fact]
        public void ReadFromJson_WhenJsonIsWhitespace_ThrowsArgumentException()
        {
            var reader = new DefaultCertificateRulesReader();

            var act = () => reader.ReadFromJson("   ");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ReadFromJson_WhenCertificateIsMissingFriendlyName_ThrowsInvalidOperationException()
        {
            var json = """
{
  "certificates": [
    {
      "operations": []
    }
  ]
}
""";

            var reader = new DefaultCertificateRulesReader();

            var act = () => reader.ReadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*friendlyName*");
        }

        [Fact]
        public void ReadFromJson_WhenCertificateFriendlyNameIsEmpty_ThrowsInvalidOperationException()
        {
            var json = """
{
  "certificates": [
    {
      "friendlyName": "   ",
      "operations": []
    }
  ]
}
""";

            var reader = new DefaultCertificateRulesReader();

            var act = () => reader.ReadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*non-empty friendlyName*");
        }

        [Fact]
        public void ReadFromJson_WhenRuleMappingKeyIsWhitespace_ThrowsArgumentException()
        {
            var json = """
{
  "certificates": [
    {
      "friendlyName": "DemoCertA",
      "operations": []
    }
  ],
  "rules": {
    "fileNameMappings": {
      "   ": "DemoCertA"
    }
  }
}
""";

            var reader = new DefaultCertificateRulesReader();

            var act = () => reader.ReadFromJson(json);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Mapping keys*");
        }

        [Fact]
        public void ReadFromJson_WhenRuleMappingValueIsWhitespace_ThrowsArgumentException()
        {
            var json = """
{
  "certificates": [
    {
      "friendlyName": "DemoCertA",
      "operations": []
    }
  ],
  "rules": {
    "fileExtensionMappings": {
      ".dll": "   "
    }
  }
}
""";

            var reader = new DefaultCertificateRulesReader();

            var act = () => reader.ReadFromJson(json);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Mapping values*");
        }

        [Fact]
        public void ReadFromJson_LoadsCertificatesAndRules()
        {
            var json = """
{
  "certificates": [
    {
      "friendlyName": "DemoCertA",
      "operations": [
        {
          "keyCode": "CP-230072",
          "operationSetCode": "Authenticode_SignTool6.3.9304_NPH_FDSHA256",
          "parameters": [
            { "parameterName": "OpusName", "parameterValue": "Microsoft" }
          ],
          "toolName": "signtool.exe",
          "toolVersion": "6.2.9304.0"
        }
      ]
    },
    {
      "friendlyName": "DemoCertB",
      "operations": []
    }
  ],
  "rules": {
    "fileNameMappings": {
      "special.dll": "DemoCertA"
    },
    "fileExtensionMappings": {
      ".dll": "DemoCertB"
    }
  }
}
""";

            var reader = new DefaultCertificateRulesReader();
            var rules = reader.ReadFromJson(json);

            rules.CertificatesByFriendlyName.ContainsKey("DemoCertA").Should().BeTrue();
            rules.FileNameMappings["special.dll"].Should().Be("DemoCertA");
            rules.FileExtensionMappings[".dll"].Should().Be("DemoCertB");
            rules.CertificatesByFriendlyName["DemoCertA"].GetProperty("operations").GetArrayLength().Should().Be(1);
        }
    }
}

