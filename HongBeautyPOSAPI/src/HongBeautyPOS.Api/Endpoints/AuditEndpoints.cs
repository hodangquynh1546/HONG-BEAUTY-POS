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

internal static class AuditEndpoints
{
    internal static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/audit-events", async (ClaimsPrincipal principal, PosDbContext db, DateOnly? from, DateOnly? to, string? kind, Guid? actorId, string? search, int? skip, int? take, CancellationToken ct) =>
    {
        if (!await IsOwner(principal, db, ct)) return Results.Forbid();
        var kinds = new[] { "EMPLOYEE", "PRODUCT", "CATEGORY", "CUSTOMER", "SUPPLIER", "SALE", "CASH", "STOCKTAKE", "PROFILE", "PASSWORD", "OWNER" };
        if (from > to || to == DateOnly.MaxValue || (kind is not null && !kinds.Contains(kind, StringComparer.Ordinal)) ||
            (skip ?? 0) < 0 || (take ?? 100) is < 1 or > 100 || search?.Length > 100)
            return Results.BadRequest(new { error = "Bộ lọc nhật ký không hợp lệ" });
        var query = db.AuditEvents.AsNoTracking().AsQueryable();
        if (from.HasValue)
        {
            var start = VietnamTime.StartOfDayUtc(from.Value);
            query = query.Where(e => e.CreatedAt >= start);
        }
        if (to.HasValue)
        {
            var end = VietnamTime.StartOfDayUtc(to.Value.AddDays(1));
            query = query.Where(e => e.CreatedAt < end);
        }
        if (kind is not null) query = query.Where(e => e.Action.StartsWith(kind + "_"));
        if (actorId.HasValue) query = query.Where(e => e.ActorId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(e =>
                db.Users.Any(u => u.Id == e.ActorId && (EF.Functions.ILike(u.FullName, pattern, "\\") || EF.Functions.ILike(u.Email, pattern, "\\"))) ||
                db.Users.Any(u => u.Id == e.SubjectId && EF.Functions.ILike(u.FullName, pattern, "\\")) ||
                db.Products.Any(p => p.Id == e.SubjectId && (EF.Functions.ILike(p.Name, pattern, "\\") || EF.Functions.ILike(p.Sku, pattern, "\\"))) ||
                db.Categories.Any(c => c.Id == e.SubjectId && EF.Functions.ILike(c.Name, pattern, "\\")) ||
                db.Customers.Any(c => c.Id == e.SubjectId && EF.Functions.ILike(c.FullName, pattern, "\\")) ||
                db.CustomerGroups.Any(g => g.Id == e.SubjectId && EF.Functions.ILike(g.Name, pattern, "\\")) ||
                db.Suppliers.Any(s => s.Id == e.SubjectId && (EF.Functions.ILike(s.Name, pattern, "\\") || EF.Functions.ILike(s.Code, pattern, "\\"))) ||
                db.SalesOrders.Any(o => o.Id == e.SubjectId && EF.Functions.ILike(o.OrderNo, pattern, "\\")) ||
                db.CashEntries.Any(c => c.Id == e.SubjectId && EF.Functions.ILike(c.Description, pattern, "\\")));
        }
        var events = await query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip(skip ?? 0).Take(take ?? 100).ToListAsync(ct);
        var ids = events.SelectMany(e => new[] { e.ActorId, e.SubjectId }).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var categories = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var customers = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        var customerGroups = await db.CustomerGroups.AsNoTracking().Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var orders = await db.SalesOrders.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var returns = await (from r in db.SalesReturns.AsNoTracking() join o in db.SalesOrders.AsNoTracking() on r.OrderId equals o.Id
            where ids.Contains(r.Id) select new { r.Id, o.OrderNo }).ToDictionaryAsync(x => x.Id, x => x.OrderNo, ct);
        var entries = await db.CashEntries.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Description, ct);
        return Results.Ok(events.Select(e => new { e.Id, e.ActorId, e.SubjectId, e.Action, e.CreatedAt, e.DetailsJson,
            ActorName = users.GetValueOrDefault(e.ActorId, "Người dùng đã ngừng hoạt động"),
            SubjectName = e.Action.StartsWith("EMPLOYEE_", StringComparison.Ordinal) ? users.GetValueOrDefault(e.SubjectId, "Nhân viên") :
                e.Action.StartsWith("PRODUCT_", StringComparison.Ordinal) ? products.GetValueOrDefault(e.SubjectId, "Sản phẩm") :
                e.Action.StartsWith("CATEGORY_", StringComparison.Ordinal) ? categories.GetValueOrDefault(e.SubjectId, "Danh mục") :
                e.Action.StartsWith("CUSTOMER_GROUP_", StringComparison.Ordinal) ? customerGroups.GetValueOrDefault(e.SubjectId, "Nhóm khách hàng") :
                e.Action.StartsWith("CUSTOMER_", StringComparison.Ordinal) ? customers.GetValueOrDefault(e.SubjectId, "Khách hàng") :
                e.Action.StartsWith("SUPPLIER_", StringComparison.Ordinal) ? suppliers.GetValueOrDefault(e.SubjectId, "Nhà cung cấp") :
                e.Action.StartsWith("SALE_RETURN_", StringComparison.Ordinal) ? "Đơn " + returns.GetValueOrDefault(e.SubjectId, "đã trả hàng") :
                e.Action.StartsWith("SALE_", StringComparison.Ordinal) ? "Đơn " + orders.GetValueOrDefault(e.SubjectId, "bán hàng") :
                e.Action.StartsWith("CASH_", StringComparison.Ordinal) ? entries.GetValueOrDefault(e.SubjectId, "Phiếu thu chi") : "Giao dịch" }));
    });
        return protectedApi;
    }
}
