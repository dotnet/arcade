// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.DotNet.DarcLib.Helpers
{
    public class PackagesHelper
    {
        public static ManifestMetadata GetManifestMetadata(string packagePath)
        {
            ManifestMetadata manifestMetadata = null;
            string tmpFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                ZipFile.ExtractToDirectory(packagePath, tmpFolder);
                string nuspecPath = Directory.GetFiles(tmpFolder, "*.nuspec").FirstOrDefault();

                if (!string.IsNullOrEmpty(nuspecPath))
                {
                    using (Stream stream = new MemoryStream(File.ReadAllBytes(nuspecPath)))
                    {
                        Manifest manifest = Manifest.ReadFrom(stream, false);
                        manifestMetadata = manifest.Metadata;
                    }
                }
            }
            finally
            {
                Directory.Delete(tmpFolder, true);
            }

            return manifestMetadata;
        }
    }
}
