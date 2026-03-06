// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Identifies a certificate used for signing.
    /// </summary>
    public interface ICertificateIdentifier
    {
        /// <summary>
        /// Gets the certificate name or identifier.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// When <c>true</c>, the certificate should be applied to matching files even if
        /// the file is already signed. Used for dual-signing scenarios (e.g. adding a
        /// Microsoft signature on top of a third-party signature).
        /// </summary>
        bool AlwaysSign { get; }
    }
}
