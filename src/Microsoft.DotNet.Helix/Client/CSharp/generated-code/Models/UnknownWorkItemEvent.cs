// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class UnknownWorkItemEvent
    {
        public UnknownWorkItemEvent()
        {
        }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Timestamp")]
        public DateTimeOffset? Timestamp { get; set; }

        [JsonProperty("Values")]
        public IImmutableDictionary<string, string> Values { get; set; }
    }
}
