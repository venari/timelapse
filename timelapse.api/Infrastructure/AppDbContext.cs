using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using timelapse.core.models;
using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;

namespace timelapse.infrastructure
{
    public class AppDbContext : DbContext
    {
        private IConfiguration _configuration;
        public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration)
        :base(options)
        {
            _configuration = configuration;
        }

        public DbSet<Device> Devices { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseNpgsql(connectionString);
        }
    }
}