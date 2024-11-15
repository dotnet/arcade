// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.DotNet.Pkg
{
    internal class PackageBundle
    {
        private bool IsNested;
        private string NameWithExtension;
        private string LocalExtractionPath;
        private string Identifier;
        private string Version;
        private string? Scripts;
        private string? Payload;

        internal PackageBundle(string localExtractionPath, string identifier, string version, string rootPkgName, bool isNested)
        {
            IsNested = isNested;
            Identifier = identifier;
            Version = version;
            NameWithExtension = rootPkgName;
            LocalExtractionPath = localExtractionPath;
            if (isNested)
            {
                NameWithExtension = Path.GetFileName(localExtractionPath);
                LocalExtractionPath = Path.Combine(Path.GetDirectoryName(localExtractionPath) ?? string.Empty, Path.GetFileNameWithoutExtension(NameWithExtension));
            }

            if (!Utilities.IsPkg(NameWithExtension))
            {
                throw new Exception($"Bundle '{NameWithExtension}' is not a .pkg file");
            }
        }

        internal void Unpack()
        {
            if (IsNested)
            {
                // The nested bundles get unpacked into a directory with a .pkg extension by `pkgutil --expand`,
                // so we remove this extension when unpacking the bundle.
                // Otherwise, there will be problems when packing the bundle due to the naming conflict
                Directory.Move(LocalExtractionPath + ".pkg", LocalExtractionPath);
            }

            Scripts = Utilities.FindInPath("Scripts", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            Payload = Utilities.FindInPath("Payload", LocalExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);

            if (!string.IsNullOrEmpty(Payload))
            {
                UnpackPayloadFile(Path.GetFullPath(Payload));
            }

            if (!IsNested)
            {
                // Zip the nested app bundles
                IEnumerable<string> nestedApps = Utilities.GetDirectories(LocalExtractionPath, "*.app", SearchOption.AllDirectories);
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

            if (IsNested)
            {
                // We now need to repack the nested bundle and remove the unpacked directory
                PkgBuild();
                Directory.Delete(LocalExtractionPath, true);
            }
        }

        internal void Pack(string dstPath)
        {
            if (!IsNested)
            {
                Scripts = Utilities.FindInPath("Scripts", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
                Payload = Utilities.FindInPath("Payload", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);

                IEnumerable<string> zippedNestedApps = Directory.GetFiles(LocalExtractionPath, "*.app", SearchOption.AllDirectories);
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

                PkgBuild(dstPath);
            }
        }

        private void PkgBuild(string dstPath = "")
        {
            string info = GenerateInfoPlist();
            string root = string.IsNullOrEmpty(Payload) ? $"{LocalExtractionPath}" : $"{Payload}";
            string args = $"--root {root} --component-plist {info} --identifier {Identifier} --version {Version} --keychain login.keychain --install-location /usr/local/share/dotnet";
            if (!string.IsNullOrEmpty(Scripts))
            {
                args += $" --scripts {Scripts}";
            }

            string outputPath = $"{LocalExtractionPath}.pkg";
            if (!IsNested)
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

        private string GenerateInfoPlist()
        {
            string root = string.IsNullOrEmpty(Payload) ? $"{LocalExtractionPath}" : $"{Payload}";
            string info = Path.Combine(Directory.GetCurrentDirectory(), "Info.plist");
            ExecuteHelper.Run("pkgbuild", $"--analyze --root {root} {info}");
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
