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

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "BuildId")]
        public string BuildId { get; set; }

        [XmlAttribute(AttributeName = "Branch")]
        public string Branch { get; set; }

        [XmlAttribute(AttributeName = "Commit")]
        public string Commit { get; set; }

        [XmlAttribute(AttributeName = "Location")]
        public string Location { get; set; }
    }

    [XmlRoot(ElementName = "Package")]
    public class Package
    {
        [XmlAttribute(AttributeName = "Id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "Version")]
        public string Version { get; set; }

        [XmlAttribute(AttributeName = "NonShipping")]
        public string NonShipping { get; set; }
    }

    [XmlRoot(ElementName = "Blob")]
    public class Blob
    {
        [XmlAttribute(AttributeName = "Id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "NonShipping")]
        public string NonShipping { get; set; }
    }
}
