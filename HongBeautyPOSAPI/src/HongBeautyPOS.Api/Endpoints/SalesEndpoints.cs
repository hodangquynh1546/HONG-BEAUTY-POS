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

internal static class SalesEndpoints
{
    internal static RouteGroupBuilder MapSalesEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/orders", async (ClaimsPrincipal principal, PosDbContext db, SalesService sales, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "sales.read", ct)) return Results.Forbid();
        return Results.Ok(await sales.ListAsync(ct));
    });
    protectedApi.MapGet("/orders/search", async (ClaimsPrincipal principal, PosDbContext db,
        string? q, string? status, DateOnly? from, DateOnly? to, Guid? sellerId, int? skip, int? take, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "sales.read", ct)) return Results.Forbid();
        var offset = skip ?? 0;
        var pageSize = take ?? 50;
        var term = q?.Trim();
        if (offset < 0 || pageSize is < 1 or > 100 || term?.Length > 100 ||
            (from.HasValue && to.HasValue && from > to) || to == DateOnly.MaxValue)
            return Results.BadRequest(new { error = "Bộ lọc đơn hàng không hợp lệ" });
        SaleStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SaleStatus>(status, true, out var value) || !Enum.IsDefined(value))
                return Results.BadRequest(new { error = "Trạng thái đơn hàng không hợp lệ" });
            parsedStatus = value;
        }
        if (sellerId.HasValue && !await IsOwner(principal, db, ct)) return Results.Forbid();
        var query = db.SalesOrders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(o => EF.Functions.ILike(o.OrderNo, pattern, "\\"));
        }
        if (parsedStatus.HasValue) query = query.Where(o => o.Status == parsedStatus.Value);
        if (sellerId.HasValue) query = query.Where(o => o.SoldBy == sellerId.Value);
        if (from.HasValue)
        {
            var startUtc = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            query = query.Where(o => o.CreatedAt >= startUtc);
        }
        if (to.HasValue)
        {
            var endUtc = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            query = query.Where(o => o.CreatedAt < endUtc);
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip(offset).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(o => new SalesOrderDto(o.Id, o.OrderNo, o.CustomerId, o.Status.ToString(),
            o.Total, o.PaidTotal, o.CreatedAt, o.ReturnedTotal, o.RefundedTotal));
        return Results.Ok(new { items, total });
    });
    protectedApi.MapPost("/orders", async (ClaimsPrincipal principal, PosDbContext db, SalesService sales, CreateSaleRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "sales.write", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        if (request.CustomerId.HasValue && !await db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.Active, ct))
            return Results.BadRequest(new { error = "Khách hàng không tồn tại" });
        try
        {
            var order = await sales.CreateAsync(request, actorId, ct);
            return Results.Created($"/api/v1/orders/{order.Id}", order);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    });
    protectedApi.MapPost("/orders/{id:guid}/payments", async (ClaimsPrincipal principal, PosDbContext db, SalesService sales, Guid id, AddPaymentRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "payments.write", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        try
        {
            var payment = await sales.AddPaymentAsync(id, request, actorId, ct);
            return payment is null ? Results.Conflict(new { error = "Đơn không hợp lệ hoặc số tiền vượt tổng" }) : Results.Created($"/api/v1/payments/{payment.Id}", payment);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    });

    protectedApi.MapGet("/orders/{id:guid}/items", async (ClaimsPrincipal principal, PosDbContext db, ReturnService returns, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "sales.read", ct)) return Results.Forbid();
        if (!await db.SalesOrders.AnyAsync(o => o.Id == id, ct)) return Results.NotFound();
        return Results.Ok(await returns.GetOrderLinesAsync(id, ct));
    });
    protectedApi.MapGet("/orders/{id:guid}/receipt", async (ClaimsPrincipal principal, PosDbContext db, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "sales.read", ct)) return Results.Forbid();
        var order = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return Results.NotFound();
        var customerName = order.CustomerId.HasValue
            ? await db.Customers.AsNoTracking().Where(c => c.Id == order.CustomerId.Value).Select(c => c.FullName).SingleOrDefaultAsync(ct)
            : null;
        var sellerName = await db.Users.AsNoTracking().Where(u => u.Id == order.SoldBy).Select(u => u.FullName).SingleOrDefaultAsync(ct);
        var items = await db.SalesOrderItems.AsNoTracking().Where(i => i.OrderId == id).OrderBy(i => i.NameSnapshot)
            .Select(i => new { i.SkuSnapshot, i.NameSnapshot, i.Quantity, i.UnitPrice, i.DiscountPercent, i.DiscountAmount,
                LineTotal = i.Quantity * i.UnitPrice - i.DiscountAmount }).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(p => p.OrderId == id).OrderBy(p => p.ReceivedAt)
            .Select(p => new { p.Method, p.Amount, p.ReceivedAt }).ToListAsync(ct);
        return Results.Ok(new { order.Id, order.OrderNo, order.CreatedAt, Status = order.Status.ToString(),
            CustomerName = customerName ?? "Khách lẻ", SellerName = sellerName ?? "—", order.Subtotal, order.Discount,
            OrderDiscount = order.Discount - items.Sum(i => i.DiscountAmount),
            order.Total, order.PaidTotal, order.ReturnedTotal, order.RefundedTotal,
            RemainingDebt = Math.Max(0, order.Total - order.ReturnedTotal - order.PaidTotal + order.RefundedTotal),
            Items = items, Payments = payments });
    });
    protectedApi.MapGet("/orders/{id:guid}/returns", async (ClaimsPrincipal principal, PosDbContext db, ReturnService returns, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "sales.read", ct)) return Results.Forbid();
        if (!await db.SalesOrders.AnyAsync(o => o.Id == id, ct)) return Results.NotFound();
        return Results.Ok(await returns.ListAsync(id, ct));
    });
    protectedApi.MapPost("/orders/{id:guid}/returns", async (ClaimsPrincipal principal, PosDbContext db, ReturnService returns, Guid id, CreateReturnRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "returns.write", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        try
        {
            var result = await returns.CreateAsync(id, request, actorId, ct);
            return Results.Created($"/api/v1/orders/{id}/returns", result);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    });
        return protectedApi;
    }
}
