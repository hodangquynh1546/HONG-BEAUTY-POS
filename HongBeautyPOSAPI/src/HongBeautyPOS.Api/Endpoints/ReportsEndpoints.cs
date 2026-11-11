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

internal static class ReportsEndpoints
{
    internal static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/reports/summary", async (ClaimsPrincipal principal, PosDbContext db, ReportService reports, DateOnly? from, DateOnly? to, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "reports.read", ct)) return Results.Forbid();
        var today = VietnamTime.Today;
        try { return Results.Ok(await reports.SummaryAsync(from ?? today, to ?? today, ct)); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    });

    protectedApi.MapGet("/reports/payment-methods", async (ClaimsPrincipal principal, PosDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "reports.read", ct)) return Results.Forbid();
        if (!VietnamTime.TryResolveRange(from, to, out var startDate, out var endDate, out var start, out var end))
            return Results.BadRequest(new { error = "Khoảng báo cáo phải từ 1 đến 367 ngày." });
        var received = await db.Payments.AsNoTracking().Where(p => p.ReceivedAt >= start && p.ReceivedAt < end)
            .GroupBy(p => p.Method).Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) }).ToListAsync(ct);
        var refunded = await db.SalesReturns.AsNoTracking().Where(r => r.CreatedAt >= start && r.CreatedAt < end)
            .GroupBy(r => r.RefundMethod).Select(g => new { Method = g.Key, Amount = g.Sum(r => r.RefundAmount) }).ToListAsync(ct);
        return Results.Ok(Enum.GetValues<PaymentMethod>().Select(method => new {
            method, received = received.FirstOrDefault(x => x.Method == method)?.Amount ?? 0m,
            refunded = refunded.FirstOrDefault(x => x.Method == method)?.Amount ?? 0m,
            net = (received.FirstOrDefault(x => x.Method == method)?.Amount ?? 0m) - (refunded.FirstOrDefault(x => x.Method == method)?.Amount ?? 0m)
        }));
    });

    protectedApi.MapGet("/reports/top-products", async (ClaimsPrincipal principal, PosDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "reports.read", ct)) return Results.Forbid();
        if (!VietnamTime.TryResolveRange(from, to, out var startDate, out var endDate, out var start, out var end))
            return Results.BadRequest(new { error = "Khoảng báo cáo phải từ 1 đến 367 ngày." });
        var sold = await (from item in db.SalesOrderItems.AsNoTracking()
            join order in db.SalesOrders.AsNoTracking() on item.OrderId equals order.Id
            where order.CreatedAt >= start && order.CreatedAt < end && order.Status != SaleStatus.Cancelled
            group item by item.ProductId into groupItems
            select new { ProductId = groupItems.Key, Quantity = groupItems.Sum(i => i.Quantity) }).ToListAsync(ct);
        var returned = await (from item in db.SalesReturnItems.AsNoTracking()
            join saleItem in db.SalesOrderItems.AsNoTracking() on item.OrderItemId equals saleItem.Id
            join saleReturn in db.SalesReturns.AsNoTracking() on item.ReturnId equals saleReturn.Id
            where saleReturn.CreatedAt >= start && saleReturn.CreatedAt < end
            group item by saleItem.ProductId into groupItems
            select new { ProductId = groupItems.Key, Quantity = groupItems.Sum(i => i.Quantity) }).ToListAsync(ct);
        var ids = sold.Select(x => x.ProductId).Concat(returned.Select(x => x.ProductId)).Distinct().ToArray();
        var names = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return Results.Ok(ids.Select(id => new {
            productId = id, productName = names.GetValueOrDefault(id, "Sản phẩm không còn sử dụng"),
            soldQuantity = sold.FirstOrDefault(x => x.ProductId == id)?.Quantity ?? 0m,
            returnedQuantity = returned.FirstOrDefault(x => x.ProductId == id)?.Quantity ?? 0m,
            netQuantity = (sold.FirstOrDefault(x => x.ProductId == id)?.Quantity ?? 0m) - (returned.FirstOrDefault(x => x.ProductId == id)?.Quantity ?? 0m)
        }).OrderByDescending(x => x.netQuantity).ThenBy(x => x.productName).Take(10));
    });

    protectedApi.MapGet("/reports/daily", async (ClaimsPrincipal principal, PosDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "reports.read", ct)) return Results.Forbid();
        if (!VietnamTime.TryResolveRange(from, to, out var startDate, out var endDate, out var start, out var end))
            return Results.BadRequest(new { error = "Khoảng báo cáo phải từ 1 đến 367 ngày." });
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            WITH events AS (
              SELECT ("CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS day,
                     "Total" AS gross, 0::numeric AS returned, 0::numeric AS received,
                     0::numeric AS refunded, 0::numeric AS other_income, 0::numeric AS expenses
              FROM sales_orders WHERE "CreatedAt" >= @start_utc AND "CreatedAt" < @end_utc AND "Status" <> 'Cancelled'
              UNION ALL
              SELECT ("CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 0, "Total", 0, "RefundAmount", 0, 0
              FROM sales_returns WHERE "CreatedAt" >= @start_utc AND "CreatedAt" < @end_utc
              UNION ALL
              SELECT ("ReceivedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 0, 0, "Amount", 0, 0, 0
              FROM payments WHERE "ReceivedAt" >= @start_utc AND "ReceivedAt" < @end_utc
              UNION ALL
              SELECT ("CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 0, 0, 0, 0,
                     CASE WHEN "Kind" = 'OtherIncome' THEN "Amount" ELSE 0 END,
                     CASE WHEN "Kind" <> 'OtherIncome' THEN "Amount" ELSE 0 END
              FROM cash_entries WHERE "CreatedAt" >= @start_utc AND "CreatedAt" < @end_utc
            )
            SELECT days.day, COALESCE(SUM(events.gross), 0), COALESCE(SUM(events.returned), 0),
                   COALESCE(SUM(events.received), 0), COALESCE(SUM(events.refunded), 0),
                   COALESCE(SUM(events.other_income), 0), COALESCE(SUM(events.expenses), 0)
            FROM generate_series(@from_date::timestamp, @to_date::timestamp, interval '1 day') AS days(day)
            LEFT JOIN events ON events.day = days.day::date
            GROUP BY days.day ORDER BY days.day
            """, connection);
        command.Parameters.Add(new NpgsqlParameter("from_date", NpgsqlDbType.Date) { Value = startDate });
        command.Parameters.Add(new NpgsqlParameter("to_date", NpgsqlDbType.Date) { Value = endDate });
        command.Parameters.Add(new NpgsqlParameter("start_utc", NpgsqlDbType.TimestampTz) { Value = start });
        command.Parameters.Add(new NpgsqlParameter("end_utc", NpgsqlDbType.TimestampTz) { Value = end });
        var points = new List<DailyReportPoint>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var gross = reader.GetDecimal(1);
            var returned = reader.GetDecimal(2);
            var received = reader.GetDecimal(3);
            var refunded = reader.GetDecimal(4);
            var otherIncome = reader.GetDecimal(5);
            var expenses = reader.GetDecimal(6);
            points.Add(new DailyReportPoint(DateOnly.FromDateTime(reader.GetDateTime(0)), gross, returned,
                gross - returned, received + otherIncome, refunded + expenses,
                received + otherIncome - refunded - expenses));
        }
        return Results.Ok(points);
    });
        return protectedApi;
    }
}
