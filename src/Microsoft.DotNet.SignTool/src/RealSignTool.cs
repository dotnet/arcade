// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// The signing implementation which actually signs binaries.
    /// </summary>
    internal sealed class RealSignTool : SignTool
    {
        private readonly string _msbuildPath;
        private readonly string _logDir;

        /// <summary>
        /// The number of bytes from the start of the <see cref="CorHeader"/> to its <see cref="CorFlags"/>.
        /// </summary>
        internal const int OffsetFromStartOfCorHeaderToFlags =
               sizeof(Int32)  // byte count
             + sizeof(Int16)  // major version
             + sizeof(Int16)  // minor version
             + sizeof(Int64); // metadata directory

        internal bool TestSign { get; }

        internal RealSignTool(SignToolArgs args) : base(args)
        {
            TestSign = args.TestSign;
            _msbuildPath = args.MSBuildPath;
            _logDir = args.LogDir;
        }

        public override bool RunMSBuild(IBuildEngine buildEngine, string projectFilePath, int round)
        {
            if (_msbuildPath == null)
            {
                return buildEngine.BuildProjectFile(projectFilePath, null, null, null);
            }

            Directory.CreateDirectory(_logDir);

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = _msbuildPath,
                Arguments = $@"""{projectFilePath}"" /bl:""{Path.Combine(_logDir, $"Signing{round}.binlog")}""",
                UseShellExecute = false,
                WorkingDirectory = TempDir,
            });

            process.WaitForExit();

            return process.ExitCode == 0;
        }

        public override void RemovePublicSign(string assemblyPath)
        {
            using (var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var peReader = new PEReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                if (!ContentUtil.IsPublicSigned(peReader))
                {
                    return;
                }

                stream.Position = peReader.PEHeaders.CorHeaderStartOffset + OffsetFromStartOfCorHeaderToFlags;
                writer.Write((UInt32)(peReader.PEHeaders.CorHeader.Flags & ~CorFlags.StrongNameSigned));
            }
        }

        public override bool VerifySignedAssembly(Stream assemblyStream)
        {
            // The assembly won't verify by design when doing test signing.
            if (TestSign)
            {
                return true;
            }

            return ContentUtil.IsAssemblyStrongNameSigned(assemblyStream);
        }
    }
}
