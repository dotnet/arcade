// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public abstract class EmbeddedTemplateTask : Task
    {
        private static readonly string _namespace = Assembly.GetExecutingAssembly().GetType(nameof(EmbeddedTemplateTask)).Namespace;

        public string Namespace => _namespace;

        /// <summary>
        /// A dictionary mapping embedded resources to file names.
        /// </summary>
        protected Dictionary<string, string> TemplateResources
        {
            get;
        } = new Dictionary<string, string>();

        /// <summary>
        /// Extracts the template files to the specified destination folder and returns the set of source files.
        /// </summary>
        /// <param name="destinationFolder">The path of the folder where the template should be extracted.</param>
        /// <returns>The extracted files of the template.</returns>
        public IEnumerable<string> Extract(string destinationFolder)
        {
            Utils.CheckNullOrEmpty(nameof(destinationFolder), destinationFolder);
            var sourceFiles = new List<string>();

            // Always clean out the destination folder to ensure we don't have stale copies of old template files.
            if (Directory.Exists(destinationFolder))
            {
                foreach (var file in Directory.EnumerateFiles(destinationFolder))
                {
                    File.Delete(file);
                }
            }
            else
            {
                Directory.CreateDirectory(destinationFolder);
            }

            foreach (string resourceName in TemplateResources.Keys)
            {
                string path = Path.Combine(destinationFolder, TemplateResources[resourceName]);

                using Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
                rs.CopyTo(fs);

                // Only specify source files. Include files (.wxi) are picked up from
                if (string.Equals(Path.GetExtension(path), ".wxs"))
                {
                    sourceFiles.Add($@"{Path.GetFullPath(path)}");
                }
            }

            return sourceFiles;
        }
    }
}
