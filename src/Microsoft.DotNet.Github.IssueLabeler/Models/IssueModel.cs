// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class IssueModel
    {
        [JsonIgnore]
        [LoadColumn(0)]
        public string Area;

        [LoadColumn(1)]
        public string Title;

        [LoadColumn(2)]
        [ColumnName("Description")]
        public string Body;

        [LoadColumn(3)]
        public float IsPR;

        [LoadColumn(4)]
        public Single NumMentions;

        [LoadColumn(5)]
        public string UserMentions;

        [NoColumn]
        public List<object> Labels { get; set; }

        [NoColumn]
        public int Number { get; set; }
    }
}
