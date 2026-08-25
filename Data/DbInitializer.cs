using System;
using System.Linq;
using System.Threading.Tasks;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HawassaUnifiedCampusEventManagementSystem.Data
{
    public static class DbInitializer
    {
        private static readonly PasswordHasher<User> _hasher = new();

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

            try
            {
                // Ensure Roles exist
                var superAdminRole = await db.roles.FirstOrDefaultAsync(r => r.name == "SuperAdmin" || r.name == "SUPERADMIN");
                if (superAdminRole == null)
                {
                    superAdminRole = new Role
                    {
                        name = "SuperAdmin",
                        description = "Full System Administrator with root clearance",
                        is_system_role = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.roles.Add(superAdminRole);
                    await db.SaveChangesAsync();
                }

                var adminRole = await db.roles.FirstOrDefaultAsync(r => r.name == "Admin" || r.name == "ADMIN");
                if (adminRole == null)
                {
                    adminRole = new Role
                    {
                        name = "Admin",
                        description = "Campus Event & Operations Administrator",
                        is_system_role = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.roles.Add(adminRole);
                    await db.SaveChangesAsync();
                }

                // 1. SUPERADMIN ACCOUNT
                var superAdminUser = await db.users.FirstOrDefaultAsync(u => u.username == "superadmin" || u.email == "superadmin@hawassa.edu.et");
                if (superAdminUser == null)
                {
                    superAdminUser = new User
                    {
                        username = "superadmin",
                        email = "superadmin@hawassa.edu.et",
                        first_name = "Master",
                        last_name = "SuperAdmin",
                        employee_id = "EMP-SA-001",
                        phone = "+251911000001",
                        account_type = "STAFF",
                        account_status = "ACTIVE",
                        email_verified = true,
                        phone_verified = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    superAdminUser.password_hash = _hasher.HashPassword(superAdminUser, "SuperAdmin@2026!");
                    db.users.Add(superAdminUser);
                    await db.SaveChangesAsync();

                    db.user_roles.Add(new user_role
                    {
                        user_id = superAdminUser.id,
                        role_id = superAdminRole.id,
                        assigned_at = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded master SuperAdmin account: superadmin@hawassa.edu.et");
                }

                // 2. ADMIN ACCOUNT (Campus Operational Administrator)
                var adminUser = await db.users.FirstOrDefaultAsync(u => u.username == "admin" || u.email == "admin@hawassa.edu.et");
                if (adminUser == null)
                {
                    adminUser = new User
                    {
                        username = "admin",
                        email = "admin@hawassa.edu.et",
                        first_name = "Campus",
                        last_name = "Administrator",
                        employee_id = "EMP-ADM-002",
                        phone = "+251911000002",
                        account_type = "STAFF",
                        account_status = "ACTIVE",
                        email_verified = true,
                        phone_verified = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    adminUser.password_hash = _hasher.HashPassword(adminUser, "Admin@2026!");
                    db.users.Add(adminUser);
                    await db.SaveChangesAsync();

                    db.user_roles.Add(new user_role
                    {
                        user_id = adminUser.id,
                        role_id = adminRole.id,
                        assigned_at = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded campus Admin account: admin@hawassa.edu.et");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("DbInitializer skipped: {Message}", ex.Message);
            }
        }
    }
}
