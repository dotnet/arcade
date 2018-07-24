using System.Collections.Generic;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class AssetData
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public List<AssetLocationData> Locations { get; set; }
    }
}
