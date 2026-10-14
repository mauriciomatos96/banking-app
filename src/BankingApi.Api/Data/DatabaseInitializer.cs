using BankingApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Api.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the schema and, if configured, an admin user.
    /// EnsureCreated keeps the demo zero-setup; a production system would use EF Core migrations.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.EnsureCreatedAsync();

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            return;

        adminEmail = adminEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == adminEmail))
            return;

        db.Users.Add(new User
        {
            FullName = "Administrator",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync();
    }
}
