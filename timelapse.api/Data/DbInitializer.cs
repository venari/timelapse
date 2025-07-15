using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        public static void Initialize(AppDbContext context, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            context.Database.EnsureCreated();

            // Create roles first
            CreateRoleIfNotExists(roleManager, RoleName_Admin);
            CreateRoleIfNotExists(roleManager, RoleName_OrganisationAdmin);

            // Create admin user with password
            CreateUserIfNotExists(userManager, "admin@enviroeyes", "admin@enviroeyes", "Enviroeyes123!", RoleName_Admin);

            // Create other users
            // CreateUserIfNotExists(userManager, "leigh@venari.co.nz", "leigh@venari.co.nz", "Enviroeyes123!", RoleName_Admin);
            // CreateUserIfNotExists(userManager, "Cameron.McDonald@zealandia.eco", "Cameron.McDonald@zealandia.eco", "Enviroeyes123!", RoleName_Admin);
            // CreateUserIfNotExists(userManager, "tom.stephenson@zealandia.eco", "tom.stephenson@zealandia.eco", "Enviroeyes123!", RoleName_Admin);


            if (!context.EventTypes.Any())
            {
                context.EventTypes.AddRange(
                    new EventType() { Name = "Test", Description = "Test" },
                    new EventType() { Name = "Sediment Discharge", Description = "Sediment Discharge" },
                    new EventType() { Name = "Water Level", Description = "Water Level" },
                    new EventType() { Name = "Paint Discharge", Description = "Paint Discharge" },
                    new EventType() { Name = "Effluent Discharge", Description = "Effluent Discharge" },
                    new EventType() { Name = "Condensation", Description = "Condensation" },
                    new EventType() { Name = "Lighting Issue", Description = "Lighting Issue" },
                    new EventType() { Name = "Other Camera Issue", Description = "Other Camera Issue" }
                );
                context.SaveChanges();
            }


            // if(!context.UserRoles.Any()){

            //     var userLeigh = context.Users.Single(u => u.UserName == "leigh@venari.co.nz");
            //     var userCameron = context.Users.Single(u => u.UserName == "Cameron.McDonald@zealandia.eco ");
            //     var userTom = context.Users.Single(u => u.UserName == "tom.stephenson@zealandia.eco");

            //     var roleAdmin = context.Roles.Single(r => r.Name == RoleName_Admin);

            //     context.UserRoles.AddRange(

            //         new Microsoft.AspNetCore.Identity.IdentityUserRole<string>(){
            //             UserId = userLeigh.Id,
            //             RoleId = roleAdmin.Id
            //         },
            //         new Microsoft.AspNetCore.Identity.IdentityUserRole<string>(){ 
            //             UserId = userCameron.Id,
            //             RoleId = roleAdmin.Id
            //         },
            //         new Microsoft.AspNetCore.Identity.IdentityUserRole<string>(){ 
            //             UserId = userTom.Id,
            //             RoleId = roleAdmin.Id
            //         }
            //     );

            //     context.SaveChanges();
            // }

        }

        private static void CreateRoleIfNotExists(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!roleManager.RoleExistsAsync(roleName).Result)
            {
                var role = new IdentityRole(roleName);
                roleManager.CreateAsync(role).Wait();
            }
        }

        private static void CreateUserIfNotExists(UserManager<AppUser> userManager, string userName, string email, string password, string roleName)
        {
            if (userManager.FindByNameAsync(userName).Result == null)
            {
                var user = new AppUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = userManager.CreateAsync(user, password).Result;
                if (result.Succeeded)
                {
                    userManager.AddToRoleAsync(user, roleName).Wait();
                }
            }
        }
    }
}



