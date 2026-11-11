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

internal static class InventoryEndpoints
{
    internal static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/stock", async (ClaimsPrincipal principal, PosDbContext db, InventoryService inventory, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.read", ct)) return Results.Forbid();
        return Results.Ok(await inventory.ListAsync(ct));
    });
    protectedApi.MapPost("/stock/adjustments", async (ClaimsPrincipal principal, PosDbContext db, InventoryService inventory, StockAdjustmentRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.adjust", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        try
        {
            var result = await inventory.AdjustAsync(request, actorId, ct);
            return result is null ? Results.Conflict(new { error = "Sản phẩm không tồn tại hoặc không đủ tồn kho" }) :
                Results.Created($"/api/v1/stock/movements/{result.MovementId}", result);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    });
    protectedApi.MapGet("/stock/movements", async (ClaimsPrincipal principal, PosDbContext db, Guid? productId, int? skip, int? take, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.read", ct)) return Results.Forbid();
        var offset = skip ?? 0;
        var pageSize = take ?? 50;
        if (offset < 0 || pageSize is < 1 or > 100) return Results.BadRequest(new { error = "Phân trang không hợp lệ" });
        var query = db.StockMovements.AsNoTracking().AsQueryable();
        if (productId.HasValue) query = query.Where(m => m.ProductId == productId.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip(offset).Take(pageSize).ToListAsync(ct);
        var productIds = rows.Select(m => m.ProductId).Distinct().ToArray();
        var actorIds = rows.Select(m => m.PerformedBy).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var users = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return Results.Ok(new { items = rows.Select(m => new { m.Id, m.ProductId,
            ProductName = products.GetValueOrDefault(m.ProductId, "Sản phẩm không còn sử dụng"),
            PerformerName = users.GetValueOrDefault(m.PerformedBy, "Người dùng đã ngừng hoạt động"),
            m.Type, m.Delta, m.Before, m.After,
            Reason = m.Type == "STOCKTAKE" ? "Kiểm kho" : m.Reason, m.CreatedAt }), total });
    });

    protectedApi.MapGet("/stocktakes", async (ClaimsPrincipal principal, PosDbContext db, StocktakeService stocktakes, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.read", ct)) return Results.Forbid();
        return Results.Ok(await stocktakes.ListAsync(ct));
    });
    protectedApi.MapGet("/stocktakes/{id:guid}", async (ClaimsPrincipal principal, PosDbContext db, StocktakeService stocktakes, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.read", ct)) return Results.Forbid();
        var session = await stocktakes.GetAsync(id, ct);
        return session is null ? Results.NotFound() : Results.Ok(session);
    });
    protectedApi.MapPost("/stocktakes", async (ClaimsPrincipal principal, PosDbContext db, StocktakeService stocktakes, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.adjust", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        try
        {
            var session = await stocktakes.CreateAsync(actorId, ct);
            return Results.Created($"/api/v1/stocktakes/{session.Id}", session);
        }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    });
    protectedApi.MapDelete("/stocktakes/{id:guid}", async (ClaimsPrincipal principal, PosDbContext db, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.adjust", ct)) return Results.Forbid();
        var session = await db.StocktakeSessions.Include(s => s.Items).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return Results.NotFound();
        if (session.Status != StocktakeStatus.Draft) return Results.Conflict(new { error = "Chỉ có thể xóa phiên kiểm kho chưa xác nhận" });
        db.StocktakeSessions.Remove(session);
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "STOCKTAKE_DRAFT_DELETED", SubjectId = session.Id,
            DetailsJson = JsonSerializer.Serialize(new { createdAt = session.CreatedAt, itemCount = session.Items.Count }) });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    });
    protectedApi.MapPut("/stocktakes/{id:guid}/items/{itemId:guid}", async (ClaimsPrincipal principal, PosDbContext db, StocktakeService stocktakes, Guid id, Guid itemId, CountStocktakeItemRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.adjust", ct)) return Results.Forbid();
        try
        {
            var session = await stocktakes.CountAsync(id, itemId, request.ActualQuantity, ct);
            return session is null ? Results.Conflict(new { error = "Phiên kiểm kho không còn mở hoặc sản phẩm không tồn tại" }) : Results.Ok(session);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    });
    protectedApi.MapPost("/stocktakes/{id:guid}/complete", async (ClaimsPrincipal principal, PosDbContext db, StocktakeService stocktakes, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "inventory.adjust", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        try
        {
            var session = await stocktakes.CompleteAsync(id, actorId, ct);
            return session is null ? Results.Conflict(new { error = "Phiên kiểm kho đã hoàn tất hoặc không tồn tại" }) : Results.Ok(session);
        }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    });
        return protectedApi;
    }
}
