// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class ArtifactSet
    {
        public List<PackageArtifactModel> Packages { get; set; } = new List<PackageArtifactModel>();

        public List<BlobArtifactModel> Blobs { get; set; } = new List<BlobArtifactModel>();

        public void Add(ArtifactSet source)
        {
            Packages.AddRange(source.Packages);
            Blobs.AddRange(source.Blobs);
        }

        public IEnumerable<XElement> ToXml() => Enumerable.Concat(
            Packages
                .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Version, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.ToXml()),
            Blobs
                .OrderBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
                .Select(b => b.ToXml()));

        public static ArtifactSet Parse(XElement xml) => new ArtifactSet
        {
            Packages = xml.Elements("Package").Select(PackageArtifactModel.Parse).ToList(),
            Blobs = xml.Elements("Blob").Select(BlobArtifactModel.Parse).ToList(),
        };
    }
}
