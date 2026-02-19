// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

#nullable enable
namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Fake signing provider for Phase 1 testing.
    /// Simulates signing by copying files and recording calls.
    /// </summary>
    public sealed class FakeSigningProvider : ISigningProvider
    {
        private readonly List<SigningCall> _calls = new();
        private readonly TimeSpan _delay;
        private readonly bool _shouldFail;
        private readonly IFileSystem _fileSystem;

        public IReadOnlyList<SigningCall> Calls => _calls;

        public FakeSigningProvider(IFileSystem? fileSystem = null, TimeSpan? delay = null, bool shouldFail = false)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _delay = delay ?? TimeSpan.Zero;
            _shouldFail = shouldFail;
        }

        public async Task<bool> SignFilesAsync(
            IReadOnlyList<(FileNode node, string outputPath)> files,
            CancellationToken cancellationToken = default)
        {
            if (_shouldFail)
            {
                return false;
            }

            foreach (var (node, outputPath) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Record the call
                _calls.Add(new SigningCall(node.Location.FilePathOnDisk!, outputPath, node.CertificateIdentifier?.Name ?? string.Empty));

                // Simulate signing delay
                if (_delay > TimeSpan.Zero)
                {
                    await Task.Delay(_delay, cancellationToken);
                }

                // Optionally append a marker to show it was "signed"
                string signedMarker = $"\n[SIGNED with {node.CertificateIdentifier?.Name}]";
                using (var appendStream = _fileSystem.GetFileStream(outputPath, FileMode.Append, FileAccess.Write))
                using (var writer = new StreamWriter(appendStream))
                {
                    await writer.WriteAsync(signedMarker);
                }
            }

            return true;
        }

        /// <summary>
        /// Reset the call history (useful for test setup).
        /// </summary>
        public void Reset()
        {
            _calls.Clear();
        }
    }

    /// <summary>
    /// Record of a signing call.
    /// </summary>
    public sealed class SigningCall
    {
        public string InputPath { get; }
        public string OutputPath { get; }
        public string Certificate { get; }
        public DateTime Timestamp { get; }

        public SigningCall(string inputPath, string outputPath, string certificate)
        {
            InputPath = inputPath;
            OutputPath = outputPath;
            Certificate = certificate;
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString() => $"{InputPath} -> {OutputPath} ({Certificate})";
    }
}
