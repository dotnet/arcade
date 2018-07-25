// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Maestro.Web.Api.v2018_07_16.Models;

namespace Maestro.Web.Data.Models
{
    public class AssetLocation
    {
        public AssetLocation()
        {
        }

        internal AssetLocation(AssetLocationData other)
        {
            Location = other.Location;
            Type = (LocationType) (int) other.Type;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Location { get; set; }

        public LocationType Type { get; set; }
    }
}
