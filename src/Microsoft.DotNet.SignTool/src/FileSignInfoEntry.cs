// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// Represents file signing information including certificate name and DoNotUnpack flag.
    /// </summary>
    internal class FileSignInfoEntry
    {
        public string CertificateName { get; }
        public bool DoNotUnpack { get; }

        public FileSignInfoEntry(string certificateName, bool doNotUnpack = false)
        {
            CertificateName = certificateName;
            DoNotUnpack = doNotUnpack;
        }
    }
}
