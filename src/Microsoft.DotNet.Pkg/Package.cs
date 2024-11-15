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

        internal static void Unpack() =>
            ProcessPackage(packing: false);

        internal static void Pack() =>
            ProcessPackage(packing: true);

        private static void ProcessPackage(bool packing)
        {
            NameWithExtension = packing ? Path.GetFileName(Processor.OutputPath) : Path.GetFileName(Processor.InputPath);
            LocalExtractionPath = packing ? Processor.InputPath : Processor.OutputPath;

            if (!Processor.IsPkg(NameWithExtension))
            {
                throw new Exception($"Package '{NameWithExtension}' is not a .pkg file");
            }

            if (!packing)
            {
                ExpandPkg();
            }

            Resources = Processor.FindInPath("Resources", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            Distribution = Processor.FindInPath("Distribution", LocalExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);
            Scripts = Processor.FindInPath("Scripts", LocalExtractionPath, isDirectory: true, searchOption: SearchOption.TopDirectoryOnly);
            string? packageInfo = Processor.FindInPath("PackageInfo", LocalExtractionPath, isDirectory: false, searchOption: SearchOption.TopDirectoryOnly);

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
                    PackPkg();
                }
            }
            else if (!string.IsNullOrEmpty(packageInfo))
            {
                // This is a single bundle package
                XElement pkgBundle = XElement.Load(packageInfo);
                ProcessBundle(pkgBundle, isNested: false, packing: packing);
            }
            else if (packing)
            {
                throw new Exception("Cannot unpack: no Distribution or PackageInfo file found in package");
            }
        }

        private static void ExpandPkg()
        {
            if (Directory.Exists(LocalExtractionPath))
            {
                Directory.Delete(LocalExtractionPath, true);
            }

            ExecuteHelper.Run("pkgutil", $"--expand {Processor.InputPath} {LocalExtractionPath}");
        }

        private static void PackPkg()
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

            if (File.Exists(Processor.OutputPath))
            {
                File.Delete(Processor.OutputPath);
            }
            args += $" {Processor.OutputPath}";

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
                bundle.Pack();
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
