// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.SignTool
{
    internal class FileExtensionSignInfo
    {
        internal static bool TryGetSignInfo(
            ITaskItem[] fileExtensionSignInfoPostBuild, 
            string fullPath,
            string barBuildId, 
            out SignInfo signInfo)
        {
            signInfo = SignInfo.Ignore;

            ITaskItem signInfoTaskItem = fileExtensionSignInfoPostBuild?.Where(
                        f => f.ItemSpec == Path.GetExtension(fullPath) &&
                        f.GetMetadata(SignToolConstants.BarBuildId) == barBuildId).FirstOrDefault();

            if (signInfoTaskItem != null)
            {
                string certName = signInfoTaskItem.GetMetadata("CertificateName");
                signInfo = certName.Equals(SignToolConstants.IgnoreFileCertificateSentinel, StringComparison.InvariantCultureIgnoreCase) ?
                        SignInfo.Ignore :
                        new SignInfo(certName);
                return true;
            }

            return false;
        }
    }
}
