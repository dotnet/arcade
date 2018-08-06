// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

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

        internal RealSignTool(SignToolArgs args, string msbuildPath, string logDir) : base(args)
        {
            TestSign = args.TestSign;
            _msbuildPath = msbuildPath;
            _logDir = logDir;
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

        /// <summary>
        /// Returns true if the PE file meets all of the pre-conditions to be Open Source Signed.
        /// Returns false and logs msbuild errors otherwise.
        /// </summary>
        private static bool IsPublicSigned(PEReader peReader)
        {
            if (!peReader.HasMetadata)
            {
                return false;
            }

            var mdReader = peReader.GetMetadataReader();
            if (!mdReader.IsAssembly)
            {
                return false;
            }

            CorHeader header = peReader.PEHeaders.CorHeader;
            return (header.Flags & CorFlags.StrongNameSigned) == CorFlags.StrongNameSigned;
        }

        public override void RemovePublicSign(string assemblyPath)
        {
            using (var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var peReader = new PEReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                if (!IsPublicSigned(peReader))
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

            using (var memoryStream = new MemoryStream())
            {
                assemblyStream.CopyTo(memoryStream);

                var byteArray = memoryStream.ToArray();
                unsafe
                {
                    fixed (byte* bytes = byteArray)
                    {
                        int outFlags;
                        return NativeMethods.StrongNameSignatureVerificationFromImage(
                            bytes,
                            byteArray.Length,
                            NativeMethods.SN_INFLAG_FORCE_VER, out outFlags) &&
                            (outFlags & NativeMethods.SN_OUTFLAG_WAS_VERIFIED) == NativeMethods.SN_OUTFLAG_WAS_VERIFIED;
                    }
                }
            }
        }

        private unsafe static class NativeMethods
        {
            public const int SN_INFLAG_FORCE_VER = 0x1;
            public const int SN_OUTFLAG_WAS_VERIFIED = 0x1;

            [DllImport("mscoree.dll", CharSet = CharSet.Unicode)]
            [PreserveSig]
            public static extern bool StrongNameSignatureVerificationFromImage(byte* bytes, int length, int inFlags, out int outFlags);
        }
    }
}
