using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using HongBeautyPOS.Api.Contracts;
using HongBeautyPOS.Api.Security;
using HongBeautyPOS.Application;
using HongBeautyPOS.Domain;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using static HongBeautyPOS.Api.Security.Access;

namespace HongBeautyPOS.Api.Endpoints;

internal static class MeEndpoints
{
    internal static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/me", async (ClaimsPrincipal principal, PosDbContext db) =>
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return Results.Unauthorized();
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id && u.Active).SingleOrDefaultAsync();
        if (user is null) return Results.Unauthorized();
        var permissions = user.Role == UserRole.Owner ? Array.Empty<string>() :
            await db.EmployeePermissions.Where(p => p.UserId == id).Select(p => p.Permission).ToArrayAsync();
        return Results.Ok(new { user.Id, user.Email, user.FullName, user.AvatarData, Role = user.Role.ToString(), Permissions = permissions });
    });
    protectedApi.MapPut("/me/profile", async (ClaimsPrincipal principal, PosDbContext db, UpdateProfileRequest request, CancellationToken ct) =>
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return Results.Unauthorized();
        var name = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return Results.BadRequest(new { error = "Tên phải có từ 1 đến 200 ký tự" });
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Active, ct);
        if (user is null) return Results.Unauthorized();
        user.FullName = name;
        try { user.AvatarData = ProductImageRules.Normalize(request.AvatarData); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        db.AuditEvents.Add(new AuditEvent { ActorId = id, Action = "PROFILE_UPDATED", SubjectId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { user.Id, user.Email, user.FullName, user.AvatarData, Role = user.Role.ToString() });
    });
    protectedApi.MapPut("/me/password", async (ClaimsPrincipal principal, PosDbContext db, ChangePasswordRequest request, CancellationToken ct) =>
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return Results.Unauthorized();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Active, ct);
        if (user is null) return Results.Unauthorized();
        if (string.IsNullOrEmpty(request.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return Results.BadRequest(new { error = "Mật khẩu hiện tại không đúng" });
        var password = request.NewPassword;
        if (!PasswordPolicy.IsStrong(password))
            return Results.BadRequest(new { error = PasswordPolicy.Requirement });
        if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return Results.BadRequest(new { error = "Mật khẩu mới phải khác mật khẩu cũ" });
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        db.AuditEvents.Add(new AuditEvent { ActorId = id, Action = "PASSWORD_CHANGED", SubjectId = id });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    });
        return protectedApi;
    }
}
