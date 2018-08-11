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
