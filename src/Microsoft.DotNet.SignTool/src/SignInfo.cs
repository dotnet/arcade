// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct SignInfo
    {
        /// <summary>
        /// Used to flag that the signing for the file is not necessary.
        /// </summary>
        public static readonly SignInfo Ignore = new SignInfo(ignoreThisFile: true, alreadySigned: false);

        /// <summary>
        /// Used to flag that the file is already signed.
        /// </summary>
        public static readonly SignInfo AlreadySigned = new SignInfo(ignoreThisFile: false, alreadySigned: true);

        /// <summary>
        /// The authenticode certificate which should be used to sign the binary. This can be null
        /// in cases where we have a zip container where the contents are signed but not the actual
        /// container itself. This is the case when dealing with nupkg files.
        /// </summary>
        internal string Certificate { get; }

        /// <summary>
        /// This will be null in the case a strong name signing is not required.
        /// </summary>
        internal string StrongName { get; }

        internal bool ShouldIgnore { get; }

        internal bool IsAlreadySigned { get; }

        public bool ShouldLocallyStrongNameSign => !string.IsNullOrEmpty(StrongName) && StrongName.EndsWith(".snk", StringComparison.OrdinalIgnoreCase);

        public bool ShouldSign => !IsAlreadySigned && !ShouldIgnore;

        public SignInfo(string certificate, string strongName, bool shouldIgnore, bool isAlreadySigned)
        {
            ShouldIgnore = shouldIgnore;
            IsAlreadySigned = isAlreadySigned;
            Certificate = certificate;
            StrongName = strongName;
        }

        private SignInfo(bool ignoreThisFile, bool alreadySigned) 
            : this(certificate: null, strongName: null, ignoreThisFile, alreadySigned)
        {
        }

        internal SignInfo(string certificate, string strongName = null)
            : this(certificate, strongName, shouldIgnore: false, isAlreadySigned: false)
        {
        }

        internal SignInfo WithCertificateName(string value)
            => new SignInfo(value, StrongName, ShouldIgnore, IsAlreadySigned);
    }
}
