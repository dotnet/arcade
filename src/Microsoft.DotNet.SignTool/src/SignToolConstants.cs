// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool
{
    internal static class SignToolConstants
    {
        public const string IgnoreFileCertificateSentinel = "None";

        // These certificate are special because they are used when we want 
        // to sign a file that is already signed.
        public const string Certificate_Microsoft3rdPartyAppComponentDual = "Microsoft3rdPartyAppComponentDual";
        public const string Certificate_Microsoft3rdPartyAppComponentSha2 = "Microsoft3rdPartyAppComponentSha2";
    }
}
