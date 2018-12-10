// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Runtime.Api;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class GitHubIssue
    {
        [Column(ordinal: "0")]
        public string ID;

        [JsonIgnore]
        [Column(ordinal: "1")]
        public string Area;

        [Column(ordinal: "2")]
        public string Title;

        [DataMember(Name = "body")]
        [Column(ordinal: "3")]
        public string Description;

        [DataMember(Name = "labels")]
        [NoColumn]
        public List<object> Labels { get; set; }

        [DataMember(Name = "milestone")]
        [NoColumn]
        public Milestone Milestone { get; set; }

        [DataMember(Name = "number")]
        [NoColumn]
        public int Number { get; set; }
    }

    public class Milestone
    {
        public int Number { get; set; }
    }
}
