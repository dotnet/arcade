// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    internal abstract class SignTool
    {
        private readonly SignToolArgs _args;
        internal readonly TaskLoggingHelper _log;
        internal string TempDir => _args.TempDir;
        internal string MicroBuildCorePath => _args.MicroBuildCorePath;

        internal string WixToolsPath => _args.WixToolsPath;
        internal string TarToolPath => _args.TarToolPath;
        internal string PkgToolPath => _args.PkgToolPath;

        internal SignTool(SignToolArgs args, TaskLoggingHelper log)
        {
            _args = args;
            _log = log;
        }

        public abstract void RemovePublicSign(string assemblyPath);

        public abstract bool LocalStrongNameSign(IBuildEngine buildEngine, int round, IEnumerable<FileSignInfo> files);

        public abstract bool VerifySignedDeb(TaskLoggingHelper log, string filePath);
        public abstract bool VerifySignedPEFile(Stream stream);
        public abstract bool VerifySignedPowerShellFile(string filePath);
        public abstract bool VerifySignedNugetFileMarker(string filePath);
        public abstract bool VerifySignedVSIXFileMarker(string filePath);
        public abstract bool VerifySignedPkgOrAppBundle(string filePath, string pkgToolPath);

        public abstract bool VerifyStrongNameSign(string fileFullPath);

        public abstract bool RunMSBuild(IBuildEngine buildEngine, string projectFilePath, string binLogPath);

        public bool Sign(IBuildEngine buildEngine, int round, IEnumerable<FileSignInfo> files)
        {
            return LocalStrongNameSign(buildEngine, round, files)
                && AuthenticodeSign(buildEngine, round, files);
        }

        private bool AuthenticodeSign(IBuildEngine buildEngine, int round, IEnumerable<FileSignInfo> filesToSign)
        {
            var signingDir = Path.Combine(_args.TempDir, "Signing");
            var nonOSXFilesToSign = filesToSign.Where(fsi => !SignToolConstants.SignableOSXExtensions.Contains(Path.GetExtension(fsi.FileName)));
            var osxFilesToSign = filesToSign.Where(fsi => SignToolConstants.SignableOSXExtensions.Contains(Path.GetExtension(fsi.FileName)));

            var nonOSXSigningStatus = true;
            var osxSigningStatus = true;

            Directory.CreateDirectory(signingDir);

            if (nonOSXFilesToSign.Any())
            {
                var nonOSXBuildFilePath = Path.Combine(signingDir, $"Round{round}.proj");
                var nonOSXProjContent = GenerateBuildFileContent(nonOSXFilesToSign);

                File.WriteAllText(nonOSXBuildFilePath, nonOSXProjContent);
                nonOSXSigningStatus = RunMSBuild(buildEngine, nonOSXBuildFilePath, Path.Combine(_args.LogDir, $"Signing{round}.binlog"));
            }

            if (osxFilesToSign.Any())
            {
                // The OSX signing target requires all files to be in the same folder.
                // Also all files on the folder will be signed using the same certificate.
                // Therefore below we group the files to be signed by certificate.
                var filesGroupedByCertificate = osxFilesToSign.GroupBy(fsi => fsi.SignInfo.Certificate);

                foreach (var osxFileGroup in filesGroupedByCertificate)
                {
                    // Map of the zip file path to the original file path
                    // This is necessary because files can have the same name
                    // so we need to zip them into random directories to avoid conflicts
                    Dictionary<string, string> zipPaths = new Dictionary<string, string>();

                    var certificate = osxFileGroup.Key;
                    var osxBuildFilePath = Path.Combine(signingDir, $"Round{round}-OSX-Cert{certificate}.proj");

                    string osxFilesZippingDir = Path.Combine(signingDir, osxFileGroup.Key);
                    Directory.CreateDirectory(osxFilesZippingDir);

                    try
                    {
                        // Zip the files
                        foreach (FileSignInfo item in osxFileGroup)
                        {
                            string zipFilePath = GetZipFilePath(osxFilesZippingDir, item.FileName);
                            zipPaths.Add(item.FullPath, zipFilePath);
                            if (item.IsAppBundle())
                            {
                                // This is already a zip file, no need to zip it again.
                                // Just rename the file to .zip and
                                // move it to the destination path.
                                File.Copy(item.FullPath, zipFilePath);
                            }
                            else
                            {
                                // https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/19841/Additional-Requirements-for-Signing-or-Notarizing-Mac-Files?anchor=example-of-using-ditto
                                var process = Process.Start(new ProcessStartInfo()
                                {
                                    FileName = "ditto",
                                    Arguments = $"-V -ck --sequesterRsrc \"{item.FullPath}\" \"{zipFilePath}\"",
                                    UseShellExecute = false,
                                    WorkingDirectory = TempDir,
                                });

                                process.WaitForExit();
                                if (process.ExitCode != 0)
                                {
                                    _log.LogError($"Failed to zip file {item.FullPath} to {zipFilePath}");
                                    return false;
                                }
                            }
                        }

                        var osxProjContent = GenerateBuildFileContent(osxFileGroup, zipPaths: zipPaths, isOSX: true);

                        File.WriteAllText(osxBuildFilePath, osxProjContent);

                        osxSigningStatus = RunMSBuild(buildEngine, osxBuildFilePath, Path.Combine(_args.LogDir, $"Signing{round}-OSX.binlog"));

                        // Unzip the files
                        foreach (KeyValuePair<string, string> zipPath in zipPaths)
                        {
                            string originalFilePath = zipPath.Key;
                            string zipFilePath = zipPath.Value;
                            if (FileSignInfo.IsAppBundle(originalFilePath))
                            {
                                // This is already a zip file, no need to unzip it.
                                // Just rename the file to its original extension
                                // and move it to the destination path.
                                File.Copy(zipFilePath, originalFilePath, overwrite: true);
                            }
                            else
                            {
                                // https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/19841/Additional-Requirements-for-Signing-or-Notarizing-Mac-Files?anchor=example-of-using-ditto
                                var process = Process.Start(new ProcessStartInfo()
                                {
                                    FileName = "ditto",
                                    Arguments = $"-V -xk \"{zipFilePath}\" \"{Path.GetDirectoryName(originalFilePath)}\"",
                                    UseShellExecute = false,
                                    WorkingDirectory = TempDir,
                                });

                                process.WaitForExit();
                                if (process.ExitCode != 0)
                                {
                                    _log.LogError($"Failed to unzip file {zipFilePath} to {originalFilePath}");
                                    return false;
                                }
                            }
                        }
                    }
                    finally
                    {
                        Directory.Delete(osxFilesZippingDir, recursive: true);
                    }
                }
            }

            return nonOSXSigningStatus && osxSigningStatus;
        }

        private string GenerateBuildFileContent(IEnumerable<FileSignInfo> filesToSign, Dictionary<string, string> zipPaths = null, bool isOSX = false)
        {
            if (isOSX && zipPaths == null)
            {
                throw new ArgumentException("zipPaths must be specified when signing OSX files.");
            }

            var builder = new StringBuilder();
            AppendLine(builder, depth: 0, text: @"<?xml version=""1.0"" encoding=""utf-8""?>");
            AppendLine(builder, depth: 0, text: @"<Project DefaultTargets=""AfterBuild"">");

            // Setup the code to get the NuGet package root.
            var signKind = _args.TestSign ? "test" : "real";
            AppendLine(builder, depth: 1, text: @"<PropertyGroup>");
            if (isOSX)
            {
                AppendLine(builder, depth: 2, text: @"<EnableCodeSigning>false</EnableCodeSigning>");
            }
            AppendLine(builder, depth: 2, text: $@"<OutDir>{_args.EnclosingDir}</OutDir>");
            AppendLine(builder, depth: 2, text: $@"<IntermediateOutputPath>{_args.TempDir}</IntermediateOutputPath>");
            AppendLine(builder, depth: 2, text: $@"<SignType>{signKind}</SignType>");
            AppendLine(builder, depth: 1, text: @"</PropertyGroup>");

            AppendLine(builder, depth: 1, text: $@"<Import Project=""{Path.Combine(MicroBuildCorePath, "build", "MicroBuild.Core.props")}"" />");

            if (isOSX)
            {
                AppendLine(builder, depth: 1, text: $@"<ItemGroup Label=""OSXPackageReferences"">");
                AppendLine(builder, depth: 2, text: $@"<PackageReference Include=""Microsoft.VisualStudioEng.MicroBuild.Core"" version=""0.4.1"" />");
                AppendLine(builder, depth: 1, text: $@"</ItemGroup>");
            }
            AppendLine(builder, depth: 1, text: $@"<ItemGroup>");

            foreach (var fileToSign in filesToSign)
            {
                string filePath = isOSX ? zipPaths[fileToSign.FullPath] : fileToSign.FullPath;
                AppendLine(builder, depth: 2, text: $@"<FilesToSign Include=""{Uri.EscapeDataString(filePath)}"">");
                AppendLine(builder, depth: 3, text: $@"<Authenticode>{fileToSign.SignInfo.Certificate}</Authenticode>");
                if (fileToSign.SignInfo.StrongName != null && !fileToSign.SignInfo.ShouldLocallyStrongNameSign)
                {
                    AppendLine(builder, depth: 3, text: $@"<StrongName>{fileToSign.SignInfo.StrongName}</StrongName>");
                }
                if(isOSX)
                {
                    AppendLine(builder, depth: 3, text: $@"<Zip>true</Zip>");
                }
                AppendLine(builder, depth: 2, text: @"</FilesToSign>");
            }

            AppendLine(builder, depth: 1, text: $@"</ItemGroup>");

            // The MicroBuild targets hook AfterBuild to do the signing hence we just make it our no-op default target
            AppendLine(builder, depth: 1, text: @"<Target Name=""AfterBuild"">");
            AppendLine(builder, depth: 2, text: @"<Message Text=""Running signing process."" />");
            AppendLine(builder, depth: 1, text: @"</Target>");

            AppendLine(builder, depth: 1, text: $@"<Import Project=""{Path.Combine(MicroBuildCorePath, "build", "MicroBuild.Core.targets")}"" />");
            AppendLine(builder, depth: 0, text: @"</Project>");

            return builder.ToString();
        }

        protected virtual string GetZipFilePath(string zipFileDir, string fileName)
        {
            string tempDir = Path.Combine(zipFileDir, Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            return Path.Combine(tempDir, Path.GetFileNameWithoutExtension(fileName) + ".zip");
        }

        private static void AppendLine(StringBuilder builder, int depth, string text)
        {
            for (int i = 0; i < depth; i++)
            {
                builder.Append("    ");
            }

            builder.AppendLine(text);
        }

        protected bool LocalStrongNameSign(FileSignInfo file)
        {
            if (!File.Exists(_args.SNBinaryPath) || !_args.SNBinaryPath.EndsWith("sn.exe"))
            {
                _log.LogError($"Found file that needs to be strong-name sign ({file.FullPath}), but path to 'sn.exe' wasn't specified.");
                return false;
            }

            _log.LogMessage($"Strong-name signing {file.FullPath} locally.");

            // sn -R <path_to_file> <path_to_snk>
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = _args.SNBinaryPath,
                Arguments = $@"-R ""{file.FullPath}"" ""{file.SignInfo.StrongName}""",
                UseShellExecute = false,
                WorkingDirectory = TempDir,
            });

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _log.LogError($"Failed to strong-name sign file {file.FullPath}");
                return false;
            }

            return true;
        }
    }
}
