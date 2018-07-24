using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Maestro.Web.Api.v2018_07_16.Models;

namespace Maestro.Web.Data.Models
{
    public class Asset
    {
        public Asset()
        {
        }

        internal Asset(AssetData other)
        {
            Name = other.Name;
            Version = other.Version;
            Locations = other.Locations.Select(l => new AssetLocation(l)).ToList();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public int BuildId { get; set; }

        public List<AssetLocation> Locations { get; set; }
    }
}
