using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure;

public static class DatabaseBootstrapper
{
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseBootstrapper");

        try
        {
            var shouldRunMigrations = config.GetValue("RUN_MIGRATIONS_ON_STARTUP", true);

            if (shouldRunMigrations && context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully.");
            }
            else if (!shouldRunMigrations)
                logger.LogInformation("Skipping database migrations because RUN_MIGRATIONS_ON_STARTUP is set to false.");

            var adminEmail = config["DEFAULT_ADMIN_EMAIL"];
            var adminPassword = config["DEFAULT_ADMIN_PASSWORD"];

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var existingUser = await userManager.FindByEmailAsync(adminEmail);
                if (existingUser == null)
                {
                    logger.LogInformation("Default admin user not found. Seeding default admin user.");
                    var adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(adminUser, adminPassword);
                    if (result.Succeeded)
                    {
                        var org = new Organization
                        {
                            Name = "Default Organization"
                        };

                        org.Members.Add(new OrganizationMember
                        {
                            UserId = adminUser.Id,
                            Role = OrganizationRole.Admin
                        });

                        context.Organizations.Add(org);
                        await context.SaveChangesAsync();

                        logger.LogInformation("Default admin user and organization created successfully.");
                    }
                    else
                    {
                        logger.LogError("Failed to create default admin user: {Errors}",
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(existingUser);
                    var resetResult = await userManager.ResetPasswordAsync(existingUser, token, adminPassword);
                    if (resetResult.Succeeded)
                        logger.LogInformation("Default admin password reset to match configuration.");
                    else
                        logger.LogError("Failed to reset default admin password: {Errors}",
                            string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying migrations or seeding the database.");
            throw;
        }
    }
}
