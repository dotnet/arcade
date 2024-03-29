// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ApiError
    {
        public ApiError(string message, string activityId, IImmutableList<string> errors)
        {
            Message = message;
            ActivityId = activityId;
            Errors = errors;
        }

        [JsonProperty("Message")]
        public string Message { get; }

        [JsonProperty("ActivityId")]
        public string ActivityId { get; }

        [JsonProperty("Errors")]
        public IImmutableList<string> Errors { get; }
    }
}
