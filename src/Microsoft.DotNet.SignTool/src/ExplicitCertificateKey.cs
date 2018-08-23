// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct ExplicitCertificateKey : IEquatable<ExplicitCertificateKey>
    {
        public readonly string FileName;
        public readonly string PublicKeyToken;
        public readonly string TargetFramework;

        public ExplicitCertificateKey(string fileName, string publicKeyToken = null, string targetFramework = null)
        {
            Debug.Assert(fileName != null);

            FileName = fileName;
            PublicKeyToken = publicKeyToken ?? "";
            TargetFramework = targetFramework ?? "";
        }

        public override bool Equals(object obj)
            => obj is ExplicitCertificateKey key && Equals(key);

        public override int GetHashCode()
            => Hash.Combine(Hash.Combine(FileName.GetHashCode(), PublicKeyToken.GetHashCode()), TargetFramework.GetHashCode());

        bool IEquatable<ExplicitCertificateKey>.Equals(ExplicitCertificateKey other)
            => FileName == other.FileName && 
            string.Equals(PublicKeyToken, other.PublicKeyToken, StringComparison.OrdinalIgnoreCase) && 
            TargetFramework == other.TargetFramework;

        public static bool operator ==(ExplicitCertificateKey key1, ExplicitCertificateKey key2) 
            => key1.Equals(key2);

        public static bool operator !=(ExplicitCertificateKey key1, ExplicitCertificateKey key2)
            => !(key1 == key2);
    }
}
