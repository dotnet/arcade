// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace RoslynTools
{
    public class RewriteOrchestratedBuildPackageVersions : Task
    {
        [Required]
        public string File { get; set; }

        public bool Overwrite { get; set; }

        public override bool Execute()
        {
            var xml = XDocument.Load(File);

            foreach (var node in xml.Descendants())
            {
                const string oldSuffix = "PackageVersion";
                const string newSuffix = "Version";

                string name = node.Name.LocalName;
                if (name.EndsWith(oldSuffix))
                {
                    node.Name = XName.Get(name.Substring(0, name.Length - oldSuffix.Length) + newSuffix, node.Name.NamespaceName);
                }
            }

            using (var stream = System.IO.File.OpenWrite(File))
            {
                stream.SetLength(0);
                xml.Save(stream);
            }

            return true;
        }
    }
}

