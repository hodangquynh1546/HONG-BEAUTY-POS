using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HongBeautyPOS.Domain;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Security;

internal static class CurrentUser
{
    public static Guid? TryGetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    /// <summary>Only call inside endpoints that already require authorization.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        principal.TryGetUserId() ?? throw new InvalidOperationException("Authenticated principal has no valid subject");
}

/// <summary>Authorization checks that always re-read the user so revoked access takes effect immediately.</summary>
internal static class Access
{
    public static async Task<bool> HasPermission(ClaimsPrincipal principal, PosDbContext db, string permission, CancellationToken ct)
    {
        if (principal.TryGetUserId() is not { } id) return false;
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id && u.Active)
            .Select(u => new { u.Role }).SingleOrDefaultAsync(ct);
        if (user is null) return false;
        return user.Role == UserRole.Owner || await db.EmployeePermissions.AnyAsync(p => p.UserId == id && p.Permission == permission, ct);
    }

    public static async Task<bool> IsOwner(ClaimsPrincipal principal, PosDbContext db, CancellationToken ct)
    {
        if (principal.TryGetUserId() is not { } id) return false;
        return await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Active && u.Role == UserRole.Owner, ct);
    }
}
