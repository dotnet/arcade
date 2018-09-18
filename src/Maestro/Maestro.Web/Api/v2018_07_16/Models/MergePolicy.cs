// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class MergePolicy
    {
        public MergePolicy()
        {
        }

        public MergePolicy(Data.Models.MergePolicyDefinition other)
        {
            Name = other.Name;
            Properties = other.Properties?.ToImmutableDictionary();
        }

        public string Name { get; set; }

        [CanBeNull]
        public IImmutableDictionary<string, JToken> Properties { get; set; }

        public Data.Models.MergePolicyDefinition ToDb()
        {
            return new Data.Models.MergePolicyDefinition
            {
                Name = Name,
                Properties = Properties?.ToDictionary(p => p.Key, p => p.Value),
            };
        }
    }
}
