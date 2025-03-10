// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class NoExtensionVerifier : AuthentiCodeVerifier
    {
        public NoExtensionVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, string.Empty) { }

        public override SignatureVerificationResult VerifySignature(string path, string parent, string virtualPath)
        {
#if NET
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!isWindows && IsExecutable(path))
            {
                return base.VerifySignature(path, parent, virtualPath);
            }

            string detailMessage = isWindows ?
                "Cannot verify files without extensions on Windows." :
                "Not an executable.";
#else
            string detailMessage = "Cannot verify files without extensions on NET Framework.";
#endif

            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent, virtualPath);
            svr.IsSkipped = true;
            svr.AddDetail(DetailKeys.Misc, detailMessage);

            return svr;
        }

        /// <summary>
        /// Determines if the file is executable.
        /// </summary>
        /// <param name="path">The path to the file.</param>
#if NET
        [UnsupportedOSPlatform("windows")]
        private bool IsExecutable(string path)
        {
            UnixFileMode mode = File.GetUnixFileMode(path);
            return mode.HasFlag(UnixFileMode.UserExecute) ||
                    mode.HasFlag(UnixFileMode.GroupExecute) ||
                    mode.HasFlag(UnixFileMode.OtherExecute);
        }
#endif
    }
}
