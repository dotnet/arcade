// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Maestro.Data.Models
{
    public class RepoInstallation
    {
        [Key]
        public string Repository { get; set; }

        public long InstallationId { get; set; }
    }
}
