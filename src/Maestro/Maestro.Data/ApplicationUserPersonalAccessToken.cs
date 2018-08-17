// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maestro.Data
{
    [Table("AspNetUserPersonalAccessTokens")]
    public class ApplicationUserPersonalAccessToken
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }
        public DateTimeOffset Created { get; set; }
        public string Hash { get; set; }
        public int ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
