// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public sealed class DefaultFileDeduplicatorTests
    {
        private static FileContentKey CreateKey(string fileName, byte seed = 0x42)
        {
            // ContentHash requires non-empty bytes.
            var bytes = ImmutableArray.Create<byte>(
                seed, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
                0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
                0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
                0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F);

            return new FileContentKey(new ContentHash(bytes), fileName);
        }

        [Fact]
        public void RegisterFile_WhenFilePathIsNullOrWhitespace_Throws()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            AssertEx.ThrowsArgumentException(
                "filePathOnDisk",
                () => sut.RegisterFile(key, filePathOnDisk: ""));
        }

        [Fact]
        public void RegisterFile_FirstRegistration_RegistersPath()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            sut.RegisterFile(key, "/p1/a.dll");

            sut.TryGetRegisteredFile(key, out string? registeredPath).Should().BeTrue();
            registeredPath.Should().Be("/p1/a.dll");
        }

        [Fact]
        public void RegisterFile_SecondRegistration_Throws()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            sut.RegisterFile(key, "/p1/a.dll");

            Action act = () => sut.RegisterFile(key, "/p2/a.dll");
            act.Should().Throw<InvalidOperationException>();

            // Registered path should remain the first observed path.
            sut.TryGetRegisteredFile(key, out string? registeredPath).Should().BeTrue();
            registeredPath.Should().Be("/p1/a.dll");
        }

        [Fact]
        public void TryGetRegisteredFile_WhenMissing_ReturnsFalse_AndOutIsNull()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("missing.dll");

            bool found = sut.TryGetRegisteredFile(key, out string? originalPath);

            found.Should().BeFalse();
            originalPath.Should().BeNull();
        }

        [Fact]
        public void TryGetSignedVersion_WhenMissing_ReturnsFalse()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            sut.TryGetSignedVersion(key, out string signedPath).Should().BeFalse();

            // The implementation uses null-forgiving on the out param; don't assert on 'signedPath' value here.
        }

        [Fact]
        public void RegisterSignedFile_WhenSignedPathIsNullOrWhitespace_Throws()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            AssertEx.ThrowsArgumentException(
                "signedPath",
                () => sut.RegisterSignedFile(key, signedPath: " "));
        }

        [Fact]
        public void RegisterSignedFile_ThenTryGetSignedVersion_ReturnsTrue_AndPathMatches()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            sut.RegisterSignedFile(key, "/signed/a.dll");

            sut.TryGetSignedVersion(key, out string signedPath).Should().BeTrue();
            signedPath.Should().Be("/signed/a.dll");
        }

        [Fact]
        public void RegisterSignedFile_CalledTwice_OverwritesSignedPath()
        {
            var sut = new DefaultFileDeduplicator();
            var key = CreateKey("a.dll");

            sut.RegisterSignedFile(key, "/signed/v1/a.dll");
            sut.RegisterSignedFile(key, "/signed/v2/a.dll");

            sut.TryGetSignedVersion(key, out string signedPath).Should().BeTrue();
            signedPath.Should().Be("/signed/v2/a.dll");
        }
    }
}
