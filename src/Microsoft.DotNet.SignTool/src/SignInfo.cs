// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.SignTool
{
    internal struct SignInfo
    {
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

        internal SignInfo(string certificate, string strongName)
        {
            Certificate = certificate;
            StrongName = strongName;
        }
    }
}
