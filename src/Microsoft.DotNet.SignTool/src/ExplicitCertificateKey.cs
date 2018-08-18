// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct ExplicitCertificateKey
    {
        public readonly string FileName;
        public readonly string PublicKeyToken;
        public readonly string TargetFramework;

        public ExplicitCertificateKey(string fileName, string publicKeyToken, string targetFramework)
        {
            FileName = fileName;
            PublicKeyToken = publicKeyToken;
            TargetFramework = targetFramework;
        }
    }
}
