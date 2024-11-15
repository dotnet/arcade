// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Pkg
{
    internal static class Package
    {
        private static string NameWithExtension = string.Empty;
        private static string LocalExtractionPath = string.Empty;
        private static string? Resources = null;
        private static string? Distribution = null;
        private static string? Scripts = null;
        private static List<PackageBundle> Bundles = new List<PackageBundle>();

        internal static void Unpack(string srcPath, string dstPath) =>
            ProcessPackage(string srcPath, string dstPath, packing: false);

        internal static void Pack(string srcPath, string dstPath) =>
            ProcessPackage(srcPath, dstPath, packing: true);

        private static void ProcessPackage(string srcPath, string dstPath, bool packing)
        {
            NameWithExtension = packing ? Path.GetFileName(dstPath) : Path.GetFileName(srcPath);
            LocalExtractionPath = packing ? srcPath : dstPath;

            if (!Utilities.IsPkg(NameWithExtension))
            {
                throw new Exception($"Package '{NameWithExtension}' is not a .pkg file");
            }

            if (!packing)
            {
                ExpandPkg(srcPath);
            }

            Resources = Utilities.FindInPath("Resources", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            Distribution = Utilities.FindInPath("Distribution", LocalExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);
            Scripts = Utilities.FindInPath("Scripts", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            string? packageInfo = Utilities.FindInPath("PackageInfo", LocalExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);

            if (!string.IsNullOrEmpty(Distribution))
            {
                var xml = XElement.Load(Distribution);
                List<XElement> pkgBundles = xml.Elements("pkg-ref").Where(e => e.Value.Trim() != "").ToList();
                if (!pkgBundles.Any())
                {
                    throw new Exception("No pkg-ref elements found in Distribution file");
                }
                foreach (var pkgBundle in pkgBundles)
                {
                    ProcessBundle(pkgBundle, isNested: true, packing: packing);
                }
                
                if (packing)
                {
                    PackPkg(dstPath);
                }
            }
            else if (!string.IsNullOrEmpty(packageInfo))
            {
                // This is a single bundle package
                XElement pkgBundle = XElement.Load(packageInfo);
                ProcessBundle(pkgBundle, isNested: false, packing: packing, dstPath);
            }
            else if (packing)
            {
                throw new Exception("Cannot unpack: no Distribution or PackageInfo file found in package");
            }
        }

        private static void ExpandPkg(string srcPath)
        {
            if (Directory.Exists(LocalExtractionPath))
            {
                Directory.Delete(LocalExtractionPath, true);
            }

            ExecuteHelper.Run("pkgutil", $"--expand {srcPath} {LocalExtractionPath}");
        }

        private static void PackPkg(string dstPath)
        {
            string args = string.Empty;
            args += $"--distribution {Distribution}";
            if (Bundles.Any())
            {
                args += $" --package-path {LocalExtractionPath}";
            }
            if (!string.IsNullOrEmpty(Resources))
            {
                args += $" --resources {Resources}";
            }
            if (!string.IsNullOrEmpty(Scripts))
            {
                args += $" --scripts {Scripts}";
            }
            if (args.Length == 0)
            {
                args += $" --root {LocalExtractionPath}";
            }

            if (File.Exists(dstPath))
            {
                File.Delete(dstPath);
            }
            args += $" {dstPath}";

            ExecuteHelper.Run("productbuild", args);
        }

        private static void ProcessBundle(XElement bundleInfo, bool isNested, bool packing)
        {
            string extractionPath = isNested ? Path.Combine(LocalExtractionPath, bundleInfo.Value.Substring(1)) : LocalExtractionPath;
            string version = bundleInfo.Attribute("version")?.Value ?? throw new Exception($"No version found in bundle file {NameWithExtension}");
            string id = GetId(bundleInfo);
            PackageBundle bundle = new PackageBundle(extractionPath, id, version, NameWithExtension, isNested);

            if (!packing)
            {
                bundle.Unpack();
            }
            else
            {
                bundle.Pack(dstPath);
            }

            if (isNested)
            {
                Bundles.Add(bundle);
            }
        }

        private static string GetId(XElement element)
        {
            string id = element.Attribute("packageIdentifier")?.Value
                ?? element.Attribute("id")?.Value
                ?? element.Attribute("identifier")?.Value
                ?? throw new Exception("No packageIdentifier or id found in XElement.");

            return id;
        }
    }
}
