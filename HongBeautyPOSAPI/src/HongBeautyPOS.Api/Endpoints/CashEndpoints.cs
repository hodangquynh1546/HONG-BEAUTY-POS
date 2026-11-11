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

internal static class CashEndpoints
{
    internal static RouteGroupBuilder MapCashEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/cash-entries", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "cash.read", ct)) return Results.Forbid();
        var entries = await db.CashEntries.AsNoTracking().OrderByDescending(e => e.CreatedAt).Take(200).ToListAsync(ct);
        var userIds = entries.Select(e => e.CreatedBy).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return Results.Ok(entries.Select(e => new { e.Id, e.Kind, e.Method, e.Amount, e.Description, e.CreatedAt,
            CreatorName = users.GetValueOrDefault(e.CreatedBy, "Người dùng đã ngừng hoạt động") }));
    });
    protectedApi.MapPost("/cash-entries", async (ClaimsPrincipal principal, PosDbContext db, CreateCashEntryRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "cash.write", ct)) return Results.Forbid();
        if (request.IdempotencyKey == Guid.Empty || !Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.Method) ||
            request.Amount <= 0 || request.Amount > 1_000_000_000_000m || string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 500)
            return Results.BadRequest(new { error = "Phiếu thu chi không hợp lệ" });
        var existing = await db.CashEntries.AsNoTracking().SingleOrDefaultAsync(e => e.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null)
            return existing.Kind == request.Kind && existing.Method == request.Method && existing.Amount == request.Amount && existing.Description == request.Description.Trim()
                ? Results.Ok(new { existing.Id, existing.Kind, existing.Method, existing.Amount, existing.Description, existing.CreatedAt })
                : Results.Conflict(new { error = "Mã giao dịch đã được dùng cho nội dung khác" });
        var entry = new CashEntry
        {
            IdempotencyKey = request.IdempotencyKey, Kind = request.Kind, Method = request.Method,
            Amount = request.Amount, Description = request.Description.Trim(),
            CreatedBy = principal.GetUserId()
        };
        db.CashEntries.Add(entry);
        db.AuditEvents.Add(new AuditEvent { ActorId = entry.CreatedBy, Action = "CASH_ENTRY_CREATED", SubjectId = entry.Id });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await db.CashEntries.AsNoTracking().SingleOrDefaultAsync(e => e.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is null) throw;
            return existing.Kind == request.Kind && existing.Method == request.Method && existing.Amount == request.Amount && existing.Description == request.Description.Trim()
                ? Results.Ok(new { existing.Id, existing.Kind, existing.Method, existing.Amount, existing.Description, existing.CreatedAt })
                : Results.Conflict(new { error = "Mã giao dịch đã được dùng cho nội dung khác" });
        }
        return Results.Created($"/api/v1/cash-entries/{entry.Id}", new { entry.Id, entry.Kind, entry.Method, entry.Amount, entry.Description, entry.CreatedAt });
    });
        return protectedApi;
    }
}
