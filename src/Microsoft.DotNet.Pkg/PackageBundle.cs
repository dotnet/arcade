// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.DotNet.Pkg
{
    internal class PackageBundle
    {
        internal void Unpack(string srcPath, bool isNested)
        {
            if (isNested)
            {
                string nameWithExtension = Path.GetFileName(srcPath);
                srcPath = Path.Combine(Path.GetDirectoryName(srcPath) ?? string.Empty, Path.GetFileNameWithoutExtension(srcPath));

                // The nested bundles get unpacked into a directory with a .pkg extension by `pkgutil --expand`,
                // so we remove this extension when unpacking the bundle.
                // Otherwise, there will be problems when packing the bundle due to the naming conflict
                Directory.Move(srcPath + ".pkg", srcPath);
            }

            string? payload = Utilities.FindInPath("Payload", srcPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly) ?? throw new Exception("Payload not found");
            UnpackPayloadFile(Path.GetFullPath(payload));

            if (!isNested)
            {
                // Zip the nested app bundles
                IEnumerable<string> nestedApps = Utilities.GetDirectories(srcPath, "*.app", SearchOption.AllDirectories);
                foreach (string app in nestedApps)
                {
                    string tempDest = $"{app}.zip";
                    AppBundle.Pack(app, tempDest);
                    Directory.Delete(app, true);

                    // Rename the zipped file to .app
                    // This is needed so that the signtool
                    // can properly identity and sign app bundles
                    File.Move(tempDest, app);
                }
            }

            if (isNested)
            {
                // We now need to repack the nested bundle and remove the unpacked directory
                PkgBuild(payload, isNested);
                Directory.Delete(srcPath, true);
            }
        }

        internal void Pack(string srcPath, string dstPath, string identifier, string version, bool isNested)
        {
            if (!isNested)
            {
                IEnumerable<string> zippedNestedApps = Directory.GetFiles(srcPath, "*.app", SearchOption.AllDirectories);
                foreach (string appZip in zippedNestedApps)
                {
                    // Unzip the .app directory
                    string tempDest = appZip + ".unzipped";
                    AppBundle.Unpack(appZip, tempDest);
                    File.Delete(appZip);

                    // Rename the unzipped directory back to .app
                    // so that it can be repacked properly
                    Directory.Move(tempDest, appZip);
                }

                PkgBuild(srcPath, identifier, version, isNested, dstPath);
            }
        }

        private void PkgBuild(string srcPath, string identifier, string version, bool isNested, string dstPath = "")
        {
            string? payload = Utilities.FindInPath("Payload", srcPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly) ?? throw new Exception("Payload not found");
            string info = GenerateInfoPlist();
            string args = $"--root {payload} --component-plist {info} --identifier {identifier} --version {version} --keychain login.keychain --install-location /usr/local/share/dotnet";

            string? scripts = Utilities.FindInPath("Scripts", srcPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            if (!string.IsNullOrEmpty(Scripts))
            {
                args += $" --scripts {scripts}";
            }

            string outputPath = $"{srcPath}.pkg";
            if (!isNested)
            {
                outputPath = dstPath;
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            args += $" {outputPath}";

            ExecuteHelper.Run("pkgbuild", args);

            File.Delete(info);
        }

        private string GenerateInfoPlist(string payload)
        {
            string info = Path.Combine(Directory.GetCurrentDirectory(), "Info.plist");
            ExecuteHelper.Run("pkgbuild", $"--analyze --root {payload} {info}");
            return info;
        }

        private void UnpackPayloadFile(string payloadFilePath)
        {
            if (!File.Exists(payloadFilePath) || !Path.GetFileName(payloadFilePath).Equals("Payload"))
            {
                throw new Exception($"Cannot unpack invalid 'Payload' file in {NameWithExtension}");
            }

            string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "tempPayloadUnpackingDir");
            Directory.CreateDirectory(tempDir);
            try
            {
                // While we're shelling out to an executable named 'tar', the "Payload" file from pkgs is not actually
                // a tar file.  It's secretly a 'pbzx' file that tar on OSX has been taught to unpack.
                // As such, while there is actually untarring / re-tarring in this file using Python libraries, we have to
                // shell out to the host machine to do this.
                ExecuteHelper.Run("tar", $"-xf {payloadFilePath}", tempDir);
            }
            finally
            {
                // Remove the payload file and replace it with
                // a directory of the same name containing the unpacked contents
                File.Delete(payloadFilePath);
                Directory.Move(tempDir, payloadFilePath);
            }
        }
    }
}
