using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SqliteMaker
{
    public class ApiUsageContext : DbContext
    {
        private readonly string _path;

        public ApiUsageContext(string path)
        {
            _path = path;
        }

        public DbSet<Api> Apis { get; set; }
        public DbSet<ApiAssembly> ApiAssemblies { get; set; }
        public DbSet<Assembly> Assemblies { get; set; }
        public DbSet<Package> Packages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Filename={_path}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiAssembly>()
                .HasKey(c => new {c.ApiId, c.AssemblyId});
        }
    }

    public class Api
    {
        [Key]
        public Guid Hash { get; set; }
        public char Kind { get; set; }
        public string Namespace { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        [Required]
        public string DocId { get; set; }
        public bool IsFiltered { get; set; }

        public List<ApiAssembly> ApiAssemblies { get; set; }

        public static Guid GetKey(string docId)
        {
            using (var md5 = MD5.Create())
            {
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(docId)));
            }
        }
    }

    public class ApiAssembly
    {
        [Required]
        public Guid ApiId { get; set; }
        public Api Api { get; set; }
        [Required]
        public int AssemblyId { get; set; }
        public Assembly Assembly { get; set; }
    }

    public class Assembly
    {
        [Key]
        public int AssemblyId { get; set; }
        public string Name { get; set; }
        public string TFMId { get; set; }
        public string TFMVersion { get; set; }

        public List<ApiAssembly> ApiAssemblies { get; set; }
        [Required]
        public Package Package { get; set; }
    }

    public class Package
    {
        [Key]
        public int PackageId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Authors { get; set; }
        public int DownloadCount { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public string Dependencies { get; set; }
        public List<Assembly> Assemblies { get; set; }
    }
}
