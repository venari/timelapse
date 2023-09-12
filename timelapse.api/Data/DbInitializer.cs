using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using timelapse.api.Areas.Identity.Data;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api.Data
{

    public static class DbInitializer
    {
        public const string RoleName_Admin = "Admin";
        public const string RoleName_OrganisationAdmin = "OrganisationAdmin";

        public static void Initialize(AppDbContext context)//, UserManager<ApplicationUser> userManager)
        {
            context.Database.EnsureCreated();
        
            if(!context.Roles.Where(r => r.Name == RoleName_Admin).Any()){
                context.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole() {Name = RoleName_Admin, NormalizedName = RoleName_Admin.ToUpper()});
                context.SaveChanges();
            }
            if(!context.Roles.Where(r => r.Name == RoleName_OrganisationAdmin).Any()){
                context.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole() {Name = RoleName_OrganisationAdmin, NormalizedName = RoleName_OrganisationAdmin.ToUpper()});
                context.SaveChanges();
            }

            if(!context.Users.Where(u => u.UserName == "leigh@venari.co.nz").Any()){
                context.Users.Add(
                    new AppUser() {UserName = "leigh@venari.co.nz", Email = "leigh@venari.co.nz", NormalizedUserName = "LEIGH@VENARI.CO.NZ", NormalizedEmail = "LEIGH@VENARI.CO.NZ", EmailConfirmed = true}
                );
                context.SaveChanges();                
            }

            if(!context.EventTypes.Any()){
                context.EventTypes.AddRange(
                    new EventType() {Name = "Test", Description = "Test"},
                    new EventType() {Name = "Sediment Discharge", Description = "Sediment Discharge"},
                    new EventType() {Name = "Water Level", Description = "Water Level"},
                    new EventType() {Name = "Paint Discharge", Description = "Paint Discharge"},
                    new EventType() {Name = "Effluent Discharge", Description = "Effluent Discharge"},
                    new EventType() {Name = "Condensation", Description = "Condensation"},
                    new EventType() {Name = "Lighting Issue", Description = "Lighting Issue"},
                    new EventType() {Name = "Other Camera Issue", Description = "Other Camera Issue"}               
                );
                context.SaveChanges();                
            }


            if(!context.UserRoles.Any()){

                var userLeigh = context.Users.Single(u => u.UserName == "leigh@venari.co.nz");

                var roleAdmin = context.Roles.Single(r => r.Name == RoleName_Admin);

                context.UserRoles.AddRange(

                    new Microsoft.AspNetCore.Identity.IdentityUserRole<string>(){
                        UserId = userLeigh.Id,
                        RoleId = roleAdmin.Id
                    }        
                );

                context.SaveChanges();
            }

        }
    }
}



