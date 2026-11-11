using HongBeautyPOS.Api.Security;
using HongBeautyPOS.Domain;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Startup;

/// <summary>Owner creation and recovery from OWNER_* configuration (CLI commands and optional first-start bootstrap).</summary>
internal static class OwnerAccount
{
    public sealed record Credentials(string Email, string Name, string Password);

    public static bool TryRead(IConfiguration configuration, out Credentials? credentials)
    {
        var email = configuration["OWNER_EMAIL"]?.Trim().ToLowerInvariant();
        var name = configuration["OWNER_NAME"]?.Trim();
        var password = configuration["OWNER_PASSWORD"];
        credentials = null;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || !PasswordPolicy.IsStrong(password)) return false;
        credentials = new Credentials(email, name, password!);
        return true;
    }

    public static async Task CreateAsync(PosDbContext db, Credentials credentials)
    {
        db.Users.Add(new User
        {
            Email = credentials.Email, FullName = credentials.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(credentials.Password), Role = UserRole.Owner
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Explicit CLI: fails loudly when an owner already exists or credentials are weak.</summary>
    public static async Task BootstrapAsync(PosDbContext db, IConfiguration configuration)
    {
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Owner)) throw new InvalidOperationException("Owner already exists");
        if (!TryRead(configuration, out var credentials))
            throw new InvalidOperationException("OWNER_EMAIL, OWNER_NAME and a strong OWNER_PASSWORD are required");
        await CreateAsync(db, credentials!);
    }

    /// <summary>Start-up variant: logs a warning instead of failing so the health check can still pass.</summary>
    public static async Task BootstrapOnStartAsync(PosDbContext db, IConfiguration configuration, ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Owner)) return;
        if (!TryRead(configuration, out var credentials))
        {
            logger.LogWarning("Owner setup is pending: set valid OWNER_EMAIL, OWNER_NAME and OWNER_PASSWORD in the service environment, then redeploy.");
            return;
        }
        await CreateAsync(db, credentials!);
    }

    public static async Task RecoverPasswordAsync(PosDbContext db, IConfiguration configuration)
    {
        var email = configuration["OWNER_EMAIL"]?.Trim().ToLowerInvariant();
        var password = configuration["OWNER_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || !PasswordPolicy.IsStrong(password))
            throw new InvalidOperationException("OWNER_EMAIL and a strong OWNER_PASSWORD are required for recovery");
        var owner = await db.Users.SingleOrDefaultAsync(u => u.Role == UserRole.Owner && u.Email == email)
            ?? throw new InvalidOperationException("Owner account matching OWNER_EMAIL was not found");
        if (BCrypt.Net.BCrypt.Verify(password, owner.PasswordHash))
            throw new InvalidOperationException("New password must differ from the current password");
        owner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        db.AuditEvents.Add(new AuditEvent { ActorId = owner.Id, Action = "OWNER_PASSWORD_RECOVERED", SubjectId = owner.Id });
        await db.SaveChangesAsync();
    }
}
