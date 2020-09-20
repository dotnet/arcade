// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.Linq;

namespace Microsoft.DotNet.SignTool
{
    internal class StrongNameInfo
    {
        internal static bool TryGetSignInfo(
            ITaskItem[] strongNameInfoPostBuild,
            string barBuildId,
            PEInfo peInfo,
            out SignInfo signInfo)
        {
            signInfo = SignInfo.Ignore;

            ITaskItem strongNameTaskItem = strongNameInfoPostBuild.Where(
                           s => s.GetMetadata("PublicKeyToken") == peInfo.PublicKeyToken &&
                           s.GetMetadata("BARBuildId") == barBuildId).FirstOrDefault();

            if (strongNameTaskItem != null)
            {
                string certificateName = strongNameTaskItem.GetMetadata("CertificateName");

                signInfo = SignToolConstants.IgnoreFileCertificateSentinel.Equals(strongNameTaskItem.ItemSpec, StringComparison.OrdinalIgnoreCase)
                ? new SignInfo(certificateName)
                : new SignInfo(certificateName, strongNameTaskItem.ItemSpec);

                return true;
            }

            return false;
        }
    }
}
