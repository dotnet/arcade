using System;
using System.Collections.Generic;
using System.Linq;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class Asset
    {
        public Asset(Data.Models.Asset other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Name = other.Name;
            Version = other.Version;
            Locations = other.Locations?.Select(al => new AssetLocation(al)).ToList();
        }

        public int Id { get; }

        public string Name { get; }

        public string Version { get; }

        public List<AssetLocation> Locations { get; }
    }
}
