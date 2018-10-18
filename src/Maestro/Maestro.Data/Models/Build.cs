// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using EntityFrameworkCore.Triggers;

namespace Maestro.Data.Models
{
    public class Build
    {
        static Build()
        {
            Triggers<Build>.Inserting += entry =>
            {
                Build build = entry.Entity;
                var context = (BuildAssetRegistryContext) entry.Context;

                context.BuildChannels.AddRange(
                    from dc in context.DefaultChannels
                    where dc.Repository == build.Repository && dc.Branch == build.Branch
                    select new BuildChannel
                    {
                        Channel = dc.Channel,
                        Build = build
                    });
            };
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Repository { get; set; }

        public string Branch { get; set; }

        public string Commit { get; set; }

        public string BuildNumber { get; set; }

        public DateTimeOffset DateProduced { get; set; }

        public List<Asset> Assets { get; set; }

        [ForeignKey("DependencyBuildId")]
        public List<Build> Dependencies { get; set; }

        public List<BuildChannel> BuildChannels { get; set; }
    }

    public class BuildChannel
    {
        public int BuildId { get; set; }
        public Build Build { get; set; }
        public int ChannelId { get; set; }
        public Channel Channel { get; set; }
    }
}
