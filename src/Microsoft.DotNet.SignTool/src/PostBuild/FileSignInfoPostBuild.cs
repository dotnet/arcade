// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.SignTool
{
    internal class FileSignInfoPostBuild
    {
        internal static bool TryGetCertificateByTargetFramework(
            ITaskItem[] fileSignInfoPostBuild,
            string fullPath,
            string barBuildId,
            PEInfo peInfo,
            out string certName
            )
        {
            certName = null;

            ITaskItem fileSignInfoTaskItem = fileSignInfoPostBuild?.Where(
                            f => Path.GetFileName(fullPath) == f.ItemSpec &&
                            f.GetMetadata("PublicKeyToken") == peInfo.PublicKeyToken &&
                            f.GetMetadata("TargetFramework") == peInfo.TargetFramework &&
                            f.GetMetadata(SignToolConstants.BarBuildId) == barBuildId).FirstOrDefault();

            if (fileSignInfoTaskItem != null)
            {
                certName = fileSignInfoTaskItem.GetMetadata("CertificateName");
                return true;
            }

            return false;
        }

        internal static bool TryGetCertificateByFileNameAndPublicKeyToken(
            ITaskItem[] fileSignInfoPostBuild,
            string fullPath,
            string barBuildId,
            PEInfo peInfo,
            out string certName
            )
        {
            certName = null;

            ITaskItem fileSignInfoTaskItem = fileSignInfoPostBuild?.Where(
                            f => Path.GetFileName(fullPath) == f.ItemSpec &&
                            f.GetMetadata("PublicKeyToken") == peInfo.PublicKeyToken &&
                            f.GetMetadata("TargetFramework") == string.Empty &&
                            f.GetMetadata(SignToolConstants.BarBuildId) == barBuildId).FirstOrDefault();

            if (fileSignInfoTaskItem != null)
            {
                certName = fileSignInfoTaskItem.GetMetadata("CertificateName");
                return true;
            }

            return false;
        }

        internal static bool TryGetCertificateByFileName(
            ITaskItem[] fileSignInfoPostBuild,
            string fullPath,
            string barBuildId,
            out string certName
            )
        {
            certName = null;

            ITaskItem fileSignInfoTaskItem = fileSignInfoPostBuild?.Where(
                            f => Path.GetFileName(fullPath) == f.ItemSpec &&
                            f.GetMetadata("PublicKeyToken") == string.Empty &&
                            f.GetMetadata("TargetFramework") == string.Empty &&
                            f.GetMetadata(SignToolConstants.BarBuildId) == barBuildId).FirstOrDefault();

            if (fileSignInfoTaskItem != null)
            {
                certName = fileSignInfoTaskItem.GetMetadata("CertificateName");
                return true;
            }

            return false;
        }
    }
}
