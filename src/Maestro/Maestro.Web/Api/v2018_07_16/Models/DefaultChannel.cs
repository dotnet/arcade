// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class DefaultChannel
    {
        public DefaultChannel([NotNull] Data.Models.DefaultChannel other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Repository = other.Repository;
            Branch = other.Branch;
            Channel = other.Channel == null ? null : new Channel(other.Channel);
        }

        public int Id { get; set; }

        [StringLength(300)]
        [Required]
        public string Repository { get; set; }

        [StringLength(100)]
        public string Branch { get; set; }

        public Channel Channel { get; set; }


        public class PostData
        {
            [StringLength(300)]
            [Required]
            public string Repository { get; set; }

            [StringLength(100)]
            [Required]
            public string Branch { get; set; }

            [Required]
            public int ChannelId { get; set; }
        }
    }
}
