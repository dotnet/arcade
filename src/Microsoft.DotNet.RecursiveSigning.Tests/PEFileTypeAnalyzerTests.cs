// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public class PEFileTypeAnalyzerTests
    {
        private readonly PEFileTypeAnalyzer _analyzer = new();

        // ── CanAnalyze ──────────────────────────────────────────────────────

        [Theory]
        [InlineData("foo.dll", true)]
        [InlineData("foo.DLL", true)]
        [InlineData("foo.exe", true)]
        [InlineData("foo.EXE", true)]
        [InlineData("foo.sys", true)]
        [InlineData("foo.ocx", true)]
        [InlineData("foo.nupkg", false)]
        [InlineData("foo.txt", false)]
        [InlineData("foo.zip", false)]
        [InlineData("foo.msi", false)]
        public void CanAnalyze_Extension(string fileName, bool expected)
        {
            _analyzer.CanAnalyze(fileName).Should().Be(expected);
        }

        // ── Analyze: unsigned PE ────────────────────────────────────────────

        [Fact]
        public async Task AnalyzeAsync_UnsignedPE_DetectsPEAndNotSigned()
        {
            // Use the test assembly itself — it is a valid PE but should not
            // have an Authenticode signature (it's a test build, not signed).
            var testAssemblyPath = typeof(PEFileTypeAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(testAssemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var info = await _analyzer.AnalyzeAsync(stream, "test.dll");

            info.ExecutableType.Should().Be(ExecutableType.PE);
            info.IsAlreadySigned.Should().BeFalse();
        }

        [Fact]
        public void Analyze_Static_UnsignedPE_DetectsPEAndNotSigned()
        {
            var testAssemblyPath = typeof(PEFileTypeAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(testAssemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var info = PEFileTypeAnalyzer.Analyze(stream);

            info.ExecutableType.Should().Be(ExecutableType.PE);
            info.IsAlreadySigned.Should().BeFalse();
        }

        // ── Analyze: not a PE ───────────────────────────────────────────────

        [Fact]
        public async Task AnalyzeAsync_NotAPE_ReturnsDefault()
        {
            // Random bytes that aren't a valid PE
            var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
            using var stream = new MemoryStream(garbage);

            var info = await _analyzer.AnalyzeAsync(stream, "garbage.dll");

            info.ExecutableType.Should().Be(ExecutableType.None);
            info.IsAlreadySigned.Should().BeFalse();
        }

        [Fact]
        public async Task AnalyzeAsync_EmptyStream_ReturnsDefault()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());

            var info = await _analyzer.AnalyzeAsync(stream, "empty.dll");

            info.ExecutableType.Should().Be(ExecutableType.None);
            info.IsAlreadySigned.Should().BeFalse();
        }

        // ── Analyze: signed PE (synthetic) ──────────────────────────────────

        [Fact]
        public async Task AnalyzeAsync_SignedPE_DetectsSigned()
        {
            // Build a minimal PE with a non-zero CertificateTableDirectory entry
            var pe = BuildMinimalPEWithCertificateTable(certSize: 8);
            using var stream = new MemoryStream(pe);

            var info = await _analyzer.AnalyzeAsync(stream, "signed.dll");

            info.ExecutableType.Should().Be(ExecutableType.PE);
            info.IsAlreadySigned.Should().BeTrue();
        }

        // ── DefaultFileAnalyzer integration ─────────────────────────────────

        [Fact]
        public async Task DefaultFileAnalyzer_WithPEAnalyzer_DetectsPE()
        {
            var analyzer = new DefaultFileAnalyzer(new[] { new PEFileTypeAnalyzer() });
            var testAssemblyPath = typeof(PEFileTypeAnalyzerTests).Assembly.Location;

            var metadata = await analyzer.AnalyzeAsync(testAssemblyPath);

            metadata.ExecutableType.Should().Be(ExecutableType.PE);
            metadata.IsAlreadySigned.Should().BeFalse();
        }

        [Fact]
        public async Task DefaultFileAnalyzer_NoMatchingAnalyzer_ReturnsBasicMetadata()
        {
            var analyzer = new DefaultFileAnalyzer(new[] { new PEFileTypeAnalyzer() });
            var stream = new MemoryStream(new byte[] { 0x50, 0x4B }); // PK header (zip-like)

            var metadata = await analyzer.AnalyzeAsync(stream, "package.nupkg");

            metadata.FileName.Should().Be("package.nupkg");
            metadata.ExecutableType.Should().Be(ExecutableType.None);
            metadata.IsAlreadySigned.Should().BeFalse();
        }

        [Fact]
        public async Task DefaultFileAnalyzer_NullAnalyzers_StillWorks()
        {
            var analyzer = new DefaultFileAnalyzer();

            var metadata = await analyzer.AnalyzeAsync(new MemoryStream(new byte[1]), "test.dll");

            metadata.FileName.Should().Be("test.dll");
            metadata.ExecutableType.Should().Be(ExecutableType.None);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a PE binary with a non-zero CertificateTableDirectory by copying
        /// the test assembly and patching the Security data directory entry.
        /// The Security entry is index 4 in the data directory array.
        /// </summary>
        private static byte[] BuildMinimalPEWithCertificateTable(int certSize)
        {
            var testAssemblyPath = typeof(PEFileTypeAnalyzerTests).Assembly.Location;
            var pe = File.ReadAllBytes(testAssemblyPath);

            // Parse the PE to find the offset of the data directory array.
            // DOS header: e_lfanew at offset 0x3C (4 bytes) → PE signature offset
            int peSignatureOffset = BitConverter.ToInt32(pe, 0x3C);
            // PE\0\0 signature (4 bytes) + COFF header (20 bytes) = optional header start
            int optionalHeaderStart = peSignatureOffset + 4 + 20;
            // Check PE32 vs PE32+ (magic at optional header start)
            ushort magic = BitConverter.ToUInt16(pe, optionalHeaderStart);
            // PE32 = 0x10B (data dirs at offset 96 from opt header start)
            // PE32+ = 0x20B (data dirs at offset 112 from opt header start)
            int dataDirectoryStart = optionalHeaderStart + (magic == 0x20B ? 112 : 96);
            // Security/Certificate table is data directory index 4, each entry = 8 bytes (RVA + Size)
            int certRvaOffset = dataDirectoryStart + (4 * 8);
            int certSizeOffset = certRvaOffset + 4;

            // Patch: set RVA to end of file and Size to certSize
            BitConverter.GetBytes((uint)(pe.Length)).CopyTo(pe, certRvaOffset);
            BitConverter.GetBytes((uint)certSize).CopyTo(pe, certSizeOffset);

            return pe;
        }
    }
}
