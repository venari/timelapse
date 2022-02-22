using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using timelapse.core.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace timelapse.infrastructure
{
    public class AppDbContext : IdentityDbContext 
    {
        private IConfiguration _configuration;
        public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration, ILogger<AppDbContext> logger)
            : base(options)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private ILogger _logger;

        public DbSet<Device> Devices { get; set; }
        public DbSet<UnregisteredDevice> UnregisteredDevices { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Telemetry> Telemetry { get; set; }
        public DbSet<Image> Images { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseNpgsql(connectionString, o => o.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention();
        }
    }
}