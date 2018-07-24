using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maestro.Web
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