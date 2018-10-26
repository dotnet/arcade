// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class AssetLocationData
    {
        public string Location { get; set; }
        public LocationType Type { get; set; }

        public Data.Models.AssetLocation ToDb()
        {
            return new Data.Models.AssetLocation
            {
                Location = Location,
                Type = (Data.Models.LocationType) (int) Type
            };
        }
    }
}
