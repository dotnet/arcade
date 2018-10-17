// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class Build
    {
        public Build([NotNull] Data.Models.Build other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Repository = other.Repository;
            Branch = other.Branch;
            Commit = other.Commit;
            BuildNumber = other.BuildNumber;
            DateProduced = other.DateProduced;
            Channels = other.BuildChannels?.Select(bc => bc.Channel)
                .Where(c => c != null)
                .Select(c => new Channel(c))
                .ToList();
            Assets = other.Assets?.Select(a => new Asset(a)).ToList();
            Dependencies = other.Dependencies?.Select(b => new BuildRef {Id = b.Id}).ToList();
        }

        public int Id { get; }

        public string Repository { get; }

        public string Branch { get; }

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
