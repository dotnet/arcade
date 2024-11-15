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
        internal static void Unpack(string srcPath, string dstPath) =>
            ProcessPackage(string srcPath, string dstPath, packing: false);

        internal static void Pack(string srcPath, string dstPath) =>
            ProcessPackage(srcPath, dstPath, packing: true);

        private static void ProcessPackage(string srcPath, string dstPath, bool packing)
        {
            string nameWithExtension = packing ? Path.GetFileName(dstPath) : Path.GetFileName(srcPath);
            string localExtractionPath = packing ? srcPath : dstPath;

            if (!packing)
            {
                ExpandPkg(srcPath, dstPath);
            }

            string? resources = Utilities.FindInPath("Resources", localExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            string? distribution = Utilities.FindInPath("Distribution", localExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);
            string? scripts = Utilities.FindInPath("Scripts", localExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            string? packageInfo = Utilities.FindInPath("PackageInfo", localExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);

            if (!string.IsNullOrEmpty(distribution))
            {
                var xml = XElement.Load(distribution);
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
                    PackPkg(srcPath, dstPath, distribution, resources, scripts);
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

        private static void ExpandPkg(string srcPath, string dstPath) =>
            ExecuteHelper.Run("pkgutil", $"--expand {srcPath} {dstPath}");

        private static void PackPkg(string srcPath, string dstPath, string distribution, string? resources, string? scripts)
        {
            string args = $"--distribution {distribution} --package-path {srcPath}";

            if (!string.IsNullOrEmpty(resources))
            {
                args += $" --resources {resources}";
            }
            if (!string.IsNullOrEmpty(scripts))
            {
                args += $" --scripts {scripts}";
            }

            if (File.Exists(dstPath))
            {
                File.Delete(dstPath);
            }
            args += $" {dstPath}";

            ExecuteHelper.Run("productbuild", args);
        }

        private static void ProcessBundle(XElement bundleInfo, string localExtractionPath, string nameWithExtension, bool isNested, bool packing)
        {
            string extractionPath = isNested ? Path.Combine(localExtractionPath, bundleInfo.Value.Substring(1)) : localExtractionPath;
            if (!packing)
            {
                bundle.Unpack(extractionPath, isNested);
            }
            else
            {
                string version = bundleInfo.Attribute("version")?.Value ?? throw new Exception($"No version found in bundle file {nameWithExtension}");
                string id = bundleInfo.Attribute("identifier")?.Value ?? throw new Exception($"No identifier found in bundle file {nameWithExtension}");
                bundle.Pack(extractionPath, dstPath, id, version, isNested)
            }
        }
    }
}
