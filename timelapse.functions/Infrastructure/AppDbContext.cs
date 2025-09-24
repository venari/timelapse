using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using timelapse.core.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using timelapse.api.Areas.Identity.Data;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;

namespace timelapse.functions.infrastructure
{
    public class AppDbContext : DbContext
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

        public DbSet<Event> Events { get; set; }
        public DbSet<EventType> EventTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Explicitly map entity names to plural table names to match existing database
            modelBuilder.Entity<Device>().ToTable("devices");
            modelBuilder.Entity<UnregisteredDevice>().ToTable("unregistered_devices");
            modelBuilder.Entity<Telemetry>().ToTable("telemetry");
            modelBuilder.Entity<Image>().ToTable("images");
            modelBuilder.Entity<Project>().ToTable("projects");
            modelBuilder.Entity<DeviceProjectContract>().ToTable("device_project_contracts");
            modelBuilder.Entity<Organisation>().ToTable("organisations");
            modelBuilder.Entity<OrganisationUserJoinEntry>().ToTable("organisation_user_join_entry");
            modelBuilder.Entity<Event>().ToTable("events");
            modelBuilder.Entity<EventType>().ToTable("event_types");

            modelBuilder.Entity<Event>()
                .HasMany(e => e.EventTypes)
                .WithMany(e => e.Events);

            modelBuilder.Entity<Telemetry>()
                .HasIndex(e => e.Timestamp);

            modelBuilder.Entity<Image>()
                .HasIndex(e => e.Timestamp);

            // modelBuilder.Entity<Event>()
            //     .HasOne(e => e.EventType);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
        }
    }
}