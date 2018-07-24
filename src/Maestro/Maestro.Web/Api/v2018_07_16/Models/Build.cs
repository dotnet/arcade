using System;
using System.Collections.Generic;
using System.Linq;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class Build
    {
        public Build(Data.Models.Build other)
        {
            Id = other.Id;
            Repository = other.Repository;
            Commit = other.Commit;
            BuildNumber = other.BuildNumber;
            DateProduced = other.DateProduced;
            Channels = other.BuildChannels?.Select(bc => bc.Channel).Select(c => new Channel(c)).ToList();
            Assets = other.Assets?.Select(a => new Asset(a)).ToList();
            Dependencies = other.Dependencies?.Select(b => new BuildRef {Id = b.Id}).ToList();
        }

        public int Id { get; }

        public string Repository { get; }

        public string Commit { get; }

        public string BuildNumber { get; }

        public DateTimeOffset DateProduced { get; }

        public List<Channel> Channels { get; }

        public List<Asset> Assets { get; }

        public List<BuildRef> Dependencies { get; }
    }

    public class BuildRef
    {
        public int Id { get; set; }
    }
}
