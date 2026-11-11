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

internal static class EmployeesEndpoints
{
    internal static RouteGroupBuilder MapEmployeeEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/employees", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await IsOwner(principal, db, ct)) return Results.Forbid();
        var employees = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Employee)
            .Include(u => u.Permissions).OrderBy(u => u.FullName).ToListAsync(ct);
        return Results.Ok(employees.Select(u => new
        {
            u.Id, u.Email, u.FullName, u.AvatarData, u.Active,
            Permissions = u.Permissions.Select(p => p.Permission).OrderBy(p => p).ToArray()
        }));
    });

    protectedApi.MapGet("/permissions", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
        await IsOwner(principal, db, ct) ? Results.Ok(PermissionRules.Allowed.OrderBy(p => p)) : Results.Forbid());

    protectedApi.MapPost("/employees", async (ClaimsPrincipal principal, PosDbContext db, CreateEmployeeRequest request, CancellationToken ct) =>
    {
        if (!await IsOwner(principal, db, ct)) return Results.Forbid();
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(email) || !PermissionRules.AreValid(request.Permissions))
            return Results.BadRequest(new { error = "Thông tin nhân viên hoặc quyền không hợp lệ" });
        if (!PasswordPolicy.IsStrong(request.Password))
            return Results.BadRequest(new { error = PasswordPolicy.Requirement });
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Results.Conflict(new { error = "Email đã được sử dụng" });
        var ownerId = principal.GetUserId();
        string? avatar;
        try { avatar = ProductImageRules.Normalize(request.AvatarData); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        var user = new User
        {
            Email = email, FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), Role = UserRole.Employee, AvatarData = avatar
        };
        foreach (var permission in request.Permissions!.Distinct(StringComparer.Ordinal))
            user.Permissions.Add(new EmployeePermission { Permission = permission, GrantedBy = ownerId });
        db.Users.Add(user);
        db.AuditEvents.Add(new AuditEvent { ActorId = ownerId, Action = "EMPLOYEE_CREATED", SubjectId = user.Id,
            DetailsJson = JsonSerializer.Serialize(new { fullName = user.FullName, email = user.Email, permissions = user.Permissions.Select(p => p.Permission).ToArray(), hasAvatar = avatar is not null }) });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/employees/{user.Id}", new { user.Id, user.Email, user.FullName });
    });

    protectedApi.MapPut("/employees/{id:guid}/permissions", async (ClaimsPrincipal principal, PosDbContext db, Guid id, ReplacePermissionsRequest request, CancellationToken ct) =>
    {
        if (!await IsOwner(principal, db, ct)) return Results.Forbid();
        if (!PermissionRules.AreValid(request.Permissions)) return Results.BadRequest(new { error = "Quyền không hợp lệ" });
        var employee = await db.Users.Include(u => u.Permissions).SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Employee, ct);
        if (employee is null) return Results.NotFound();
        var ownerId = principal.GetUserId();
        var oldPermissions = employee.Permissions.Select(p => p.Permission).OrderBy(p => p).ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.EmployeePermissions.RemoveRange(employee.Permissions);
        employee.Permissions.Clear();
        await db.SaveChangesAsync(ct);
        foreach (var permission in request.Permissions!.Distinct(StringComparer.Ordinal))
            employee.Permissions.Add(new EmployeePermission { Permission = permission, GrantedBy = ownerId });
        db.AuditEvents.Add(new AuditEvent { ActorId = ownerId, Action = "EMPLOYEE_PERMISSIONS_REPLACED", SubjectId = employee.Id,
            DetailsJson = JsonSerializer.Serialize(new { oldPermissions, newPermissions = employee.Permissions.Select(p => p.Permission).OrderBy(p => p).ToArray() }) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new { employee.Id, Permissions = employee.Permissions.Select(p => p.Permission) });
    });

    protectedApi.MapPatch("/employees/{id:guid}/status", async (ClaimsPrincipal principal, PosDbContext db, Guid id, SetEmployeeStatusRequest request, CancellationToken ct) =>
    {
        if (!await IsOwner(principal, db, ct)) return Results.Forbid();
        var employee = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Employee, ct);
        if (employee is null) return Results.NotFound();
        employee.Active = request.Active;
        var ownerId = principal.GetUserId();
        db.AuditEvents.Add(new AuditEvent { ActorId = ownerId, Action = request.Active ? "EMPLOYEE_ACTIVATED" : "EMPLOYEE_DEACTIVATED", SubjectId = employee.Id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { employee.Id, employee.Active });
    });

    protectedApi.MapPut("/employees/{id:guid}/password", async (ClaimsPrincipal principal, PosDbContext db, Guid id, ResetEmployeePasswordRequest request, CancellationToken ct) =>
    {
        if (!await IsOwner(principal, db, ct)) return Results.Forbid();
        var password = request.Password;
        if (!PasswordPolicy.IsStrong(password))
            return Results.BadRequest(new { error = PasswordPolicy.Requirement });
        var employee = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Employee, ct);
        if (employee is null) return Results.NotFound();
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "EMPLOYEE_PASSWORD_RESET", SubjectId = employee.Id });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    });
        return protectedApi;
    }
}
