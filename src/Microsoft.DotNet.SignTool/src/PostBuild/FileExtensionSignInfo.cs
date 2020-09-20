// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
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

            ITaskItem signInfoTaskItem = fileExtensionSignInfoPostBuild.Where(
                        f => f.ItemSpec == Path.GetExtension(fullPath) && 
                        f.GetMetadata("BARBuildId") == barBuildId).FirstOrDefault();

            if (signInfoTaskItem != null)
            {
                signInfo = new SignInfo(signInfoTaskItem.GetMetadata("CertificateName"));
                return true;
            }

            return false;
        }
    }
}
