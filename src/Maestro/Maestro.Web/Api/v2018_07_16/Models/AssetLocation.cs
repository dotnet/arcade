using System;
using JetBrains.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class AssetLocation
    {
        public AssetLocation([NotNull] Data.Models.AssetLocation other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Location = other.Location;
            Type = (LocationType) (int) other.Type;
        }

        public int Id { get; }
        public string Location { get; }
        public LocationType Type { get; }
    }
}
