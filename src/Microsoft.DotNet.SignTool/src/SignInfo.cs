// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool
{
    internal class SignInfo
    {
        /// <summary>
        /// Used to flag the state when no information about signature is available.
        /// </summary>
        public static SignInfo Empty = new SignInfo(empty: true, ignoreThisFile: false, alreadySigned: false);

        /// <summary>
        /// Used to flag that the signing for the file is not necessary.
        /// </summary>
        public static SignInfo Ignore = new SignInfo(empty: false, ignoreThisFile: true, alreadySigned: false);

        /// <summary>
        /// Used to flag that the the file is already signed.
        /// </summary>
        public static SignInfo AlreadySigned = new SignInfo(empty: false, ignoreThisFile: false, alreadySigned: true);

        /// <summary>
        /// The authenticode certificate which should be used to sign the binary. This can be null
        /// in cases where we have a zip container where the contents are signed but not the actual
        /// container itself. This is the case when dealing with nupkg files.
        /// </summary>
        internal string Certificate { get; set; }

        /// <summary>
        /// This will be null in the case a strong name signing is not required.
        /// </summary>
        internal string StrongName { get; set; }

        internal bool ShouldIgnore { get; private set; }

        internal bool IsAlreadySigned { get; private set; }

        internal bool IsEmpty { get; private set; }

        public bool ShouldSign => !IsEmpty && !IsAlreadySigned && !ShouldIgnore;

        private SignInfo(bool empty, bool ignoreThisFile, bool alreadySigned)
        {
            ShouldIgnore = ignoreThisFile;
            IsEmpty = empty;
            IsAlreadySigned = alreadySigned;
        }

        internal SignInfo(string certificate, string strongName)
        {
            Certificate = certificate;
            StrongName = strongName;
        }

        internal SignInfo(SignInfo signInfo)
        {
            ShouldIgnore = signInfo.ShouldIgnore;
            IsEmpty = signInfo.IsEmpty;
            IsAlreadySigned = signInfo.IsAlreadySigned;
            Certificate = signInfo.Certificate;
            StrongName = signInfo.StrongName;
        }

        public override string ToString()
        {
            return $"Empty: {IsEmpty}; ShouldSign: {ShouldSign}; IsAlreadySigned: {IsAlreadySigned}; ShouldIgnore: {ShouldIgnore}; StrongName: {StrongName}; Certificate: {Certificate}; ";
        }
    }
}
