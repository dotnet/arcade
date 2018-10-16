// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class AssetData
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public List<AssetLocationData> Locations { get; set; }

        public Data.Models.Asset ToDb()
        {
            return new Data.Models.Asset
            {
                Name = Name,
                Version = Version,
                Locations = Locations?.Select(l => l.ToDb()).ToList() ?? new List<Data.Models.AssetLocation>()
            };
        }
    }
}
