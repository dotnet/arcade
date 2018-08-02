// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class FileSignInfo
    {
        internal FileName FileName { get; }

        internal SignInfo SignInfo { get; }

        /// <summary>
        /// The authenticode certificate which should be used to sign the binary.
        /// </summary>
        internal string Certificate => SignInfo.Certificate;

        /// <summary>
        /// This will be null in the case a strong name signing is not required.
        /// </summary>
        internal string StrongName => SignInfo.StrongName;

        internal bool IsEmpty => Certificate == null && StrongName == null;

        internal FileSignInfo(FileName name, SignInfo fileSignData)
        {
            Debug.Assert(name.IsAssembly || fileSignData.StrongName == null);

            FileName = name;
            SignInfo = fileSignData;
        }
    }
}
