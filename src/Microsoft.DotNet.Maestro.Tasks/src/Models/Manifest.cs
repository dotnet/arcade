// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Maestro.Tasks
{
    [XmlRoot(ElementName = "Build")]
    public class Manifest
    {
        [XmlElement(ElementName = "Package")]
        public List<Package> Packages { get; set; }

        [XmlElement(ElementName = "Blob")]
        public List<Blob> Blobs { get; set; }

        public string Commit { get; set; }

        public string Branch { get; set; }
       
        public string BuildId { get; set; }

        public string Name { get; set; }

        public string Location { get; set; }
    }

    public class Package
    {
        public string NonShipping { get; set; }

        public string Version { get; set; }

        public string Id { get; set; }
    }

    public class Blob
    {
        public string Id { get; set; }
    }
}
