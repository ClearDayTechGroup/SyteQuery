using Microsoft.EntityFrameworkCore;
using SyteQuery.Features.Common;
using SyteQuery.Features.Database.Entities;

namespace SyteQuery.Features.Database.Data;

public static class SeedDatabase
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        // Apply pending migrations - creates the schema on first run and upgrades existing
        // databases in place.
        await context.Database.MigrateAsync();

        // Seed the single local user (single-user desktop app - no login/registration)
        await SeedLocalUserAsync(context);
    }

    private static async Task SeedLocalUserAsync(ApplicationDbContext context)
    {
        // No login/registration in this app - there's exactly one user, seeded directly
        // via EF (not UserManager - no password/roles/email-confirmation needed) so that
        // the existing UserId-scoped tables (environments, history, snippets, jobs, etc.)
        // keep working unchanged. See LocalUser.
        var existingUser = await context.Users.FindAsync(LocalUser.Id);
        if (existingUser == null)
        {
            context.Users.Add(new ApplicationUser
            {
                Id = LocalUser.Id,
                UserName = "local",
                Email = "local@sytequery.local"
            });
            await context.SaveChangesAsync();
            Console.WriteLine("[SEED] Local user created");
        }

        var existingProfile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == LocalUser.Id);
        if (existingProfile == null)
        {
            context.UserProfiles.Add(new UserProfile
            {
                UserId = LocalUser.Id,
                FirstName = "Local",
                LastName = "User",
                IsActive = true
            });
            await context.SaveChangesAsync();
            Console.WriteLine("[SEED] Local user profile created");
        }
    }
}
