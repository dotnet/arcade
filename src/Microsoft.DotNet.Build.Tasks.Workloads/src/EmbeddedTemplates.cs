// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    internal class EmbeddedTemplates
    {
        internal static TaskLoggingHelper Log;

        private static readonly string s_namespace = "";

        private static readonly Dictionary<string, string> _templateResources = new();

        public static string Extract(string filename, string destinationFolder)
        {
            return Extract(filename, destinationFolder, filename);
        }

        public static string Extract(string filename, string destinationFolder, string destinationFilename)
        {
            if (!_templateResources.TryGetValue(filename, out string resourceName))
            {
                throw new KeyNotFoundException($"No template for '{filename}' exists.");
            }

            // Clean out stale files, just to be safe.
            string destinationPath = Path.Combine(destinationFolder, destinationFilename);
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            else
            {
                Directory.CreateDirectory(destinationFolder);
            }

            using Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            using FileStream fs = new(destinationPath, FileMode.Create, FileAccess.Write);

            if (rs != null)
            {
                rs.CopyTo(fs);
                Log?.LogMessage(MessageImportance.Low, $"Resource '{resourceName}' extracted to '{destinationPath}");
            }
            else
            {
                Log?.LogMessage(MessageImportance.Low, $"Unable to find resource: {resourceName}");
            }

            return destinationPath;
        }

        static EmbeddedTemplates()
        {
            s_namespace = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            _templateResources = new()
            {
                { "DependencyProvider.wxs", $"{s_namespace}.MsiTemplate.DependencyProvider.wxs" },
                { "Directories.wxs", $"{s_namespace}.MsiTemplate.Directories.wxs" },
                { "dotnethome_x64.wxs", $"{s_namespace}.MsiTemplate.dotnethome_x64.wxs" },
                { "ManifestProduct.wxs", $"{s_namespace}.MsiTemplate.ManifestProduct.wxs" },
                { "Product.wxs", $"{s_namespace}.MsiTemplate.Product.wxs" },
                { "Registry.wxs", $"{s_namespace}.MsiTemplate.Registry.wxs" },
                { "Variables.wxi", $"{s_namespace}.MsiTemplate.Variables.wxi" },

                { $"msi.swr", $"{s_namespace}.SwixTemplate.msi.swr" },
                { $"msi.swixproj", $"{s_namespace}.SwixTemplate.msi.swixproj"},
                { $"component.swr", $"{s_namespace}.SwixTemplate.component.swr" },
                { $"component.res.swr", $"{s_namespace}.SwixTemplate.component.res.swr" },
                { $"component.swixproj", $"{s_namespace}.SwixTemplate.component.swixproj" },
                { $"manifest.vsmanproj", $"{s_namespace}.SwixTempalte.manifest.vsmanproj"},

                { "Icon.png", $"{s_namespace}.Misc.Icon.png"},
                { "LICENSE.TXT", $"{s_namespace}.Misc.LICENSE.TXT"}
            };
        }
    }
}
