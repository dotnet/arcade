// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffSide
    {
        public DiffSide(DiffVersion version, DiffDocument document)
        {
            Version = version;
            Document = document;
        }

        public DiffVersion Version { get; private set; }
        public DiffDocument Document { get; private set; }
    }
}
