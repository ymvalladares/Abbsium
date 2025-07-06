using Microsoft.AspNetCore.Identity;
using Server.Entitys;

namespace Server.Services
{
    public static class DataSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // Usa tu clase personalizada User_data
            var userManager = serviceProvider.GetRequiredService<UserManager<User_data>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles = { "Admin", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Información del usuario administrador
            var adminEmail = "yordan.j.martinez@gmail.com";
            var adminUserName = "ymvalladares";
            var adminPassword = "Yordan.2020";

            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            User_data adminUser;

            if (existingAdmin == null)
            {
                adminUser = new User_data
                {
                    UserName = adminUserName,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to create admin user: {errors}");
                }
            }
            else
            {
                adminUser = existingAdmin;
            }

            // Asignar el rol Admin si aún no lo tiene
            var rolesForUser = await userManager.GetRolesAsync(adminUser);
            if (!rolesForUser.Contains("Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to assign role to admin: {errors}");
                }
            }

        }
    }
}
