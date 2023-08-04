using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using timelapse.core.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
using timelapse.api.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace timelapse.infrastructure
{
    public class AppDbContext : IdentityDbContext<AppUser>
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
        public DbSet<Telemetry> Telemetry { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<DeviceProjectContract> DeviceProjectContracts { get; set; }
        public DbSet<Organisation> Organisations { get; set; }
        public DbSet<OrganisationUserJoinEntry> OrganisationUserJoinEntry { get; set; } // DEVDO refactor code to change ORganisationUserJoinEntry to OrganisationUserJoinEntries

        public DbSet<Event> Events {get; set;}

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // _logger.LogInformation("OnConfiguring 4");
            // _logger.LogInformation("_configuration[\"ConnectionStrings:DefaultConnection\"]");
            // _logger.LogInformation(_configuration["ConnectionStrings:DefaultConnection"]);
            // _logger.LogInformation("_configuration.GetConnectionString(\"DefaultConnection\")");
            // _logger.LogInformation(_configuration.GetConnectionString("DefaultConnection"));

            // _logger.LogInformation("_configuration[\"POSTGRESQLCONNSTR_DefaultConnection\"]");
            // _logger.LogInformation(_configuration["POSTGRESQLCONNSTR_DefaultConnection"]);
            // _logger.LogInformation("_configuration[\"KeyVaultName\"]");
            // _logger.LogInformation(_configuration["KeyVaultName"]);

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            // _logger.LogInformation(connectionString);
            optionsBuilder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
        }
    }
}