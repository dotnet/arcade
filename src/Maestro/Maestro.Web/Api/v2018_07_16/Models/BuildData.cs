// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class BuildData
    {
        [Required]
        public string Repository { get; set; }

        [Required]
        public string Commit { get; set; }

        [Required]
        public string BuildNumber { get; set; }

        public List<AssetData> Assets { get; set; }

        public List<int> Dependencies { get; set; }
    }
}
