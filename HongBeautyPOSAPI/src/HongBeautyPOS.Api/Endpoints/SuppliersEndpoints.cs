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

internal static class SuppliersEndpoints
{
    internal static RouteGroupBuilder MapSupplierEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/suppliers", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "suppliers.read", ct)) return Results.Forbid();
        return Results.Ok(await db.Suppliers.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Code, s.Name, s.Phone, s.Email, s.Active }).Take(200).ToListAsync(ct));
    });
    protectedApi.MapPost("/suppliers", async (ClaimsPrincipal principal, PosDbContext db, CreateSupplierRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "suppliers.write", ct)) return Results.Forbid();
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 80 || string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Results.BadRequest(new { error = "Thông tin nhà cung cấp không hợp lệ" });
        if (await db.Suppliers.AnyAsync(s => s.Code == code, ct)) return Results.Conflict(new { error = "Mã nhà cung cấp đã tồn tại" });
        var supplier = new Supplier { Code = code, Name = name, Phone = request.Phone?.Trim(), Email = request.Email?.Trim().ToLowerInvariant() };
        if (supplier.Phone?.Length > 40 || supplier.Email?.Length > 320) return Results.BadRequest(new { error = "Thông tin liên hệ quá dài" });
        db.Suppliers.Add(supplier);
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "SUPPLIER_CREATED", SubjectId = supplier.Id,
            DetailsJson = JsonSerializer.Serialize(new { supplier.Code, supplier.Name, supplier.Phone, supplier.Email }) });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/suppliers/{supplier.Id}", new { supplier.Id, supplier.Code, supplier.Name, supplier.Phone, supplier.Email });
    });
    protectedApi.MapPut("/suppliers/{id:guid}", async (Guid id, ClaimsPrincipal principal, PosDbContext db, CreateSupplierRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "suppliers.write", ct)) return Results.Forbid();
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var phone = request.Phone?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 80 || string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            phone?.Length > 40 || email?.Length > 320)
            return Results.BadRequest(new { error = "Thông tin nhà cung cấp không hợp lệ" });
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        if (await db.Suppliers.AnyAsync(s => s.Code == code && s.Id != id, ct))
            return Results.Conflict(new { error = "Mã nhà cung cấp đã tồn tại" });
        var oldSupplier = new { supplier.Code, supplier.Name, supplier.Phone, supplier.Email };
        supplier.Code = code;
        supplier.Name = name;
        supplier.Phone = phone;
        supplier.Email = email;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "SUPPLIER_UPDATED", SubjectId = id, DetailsJson = JsonSerializer.Serialize(new { before = oldSupplier, after = new { supplier.Code, supplier.Name, supplier.Phone, supplier.Email } }) });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { supplier.Id, supplier.Code, supplier.Name, supplier.Phone, supplier.Email, supplier.Active });
    });
    protectedApi.MapPatch("/suppliers/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "suppliers.write", ct)) return Results.Forbid();
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        supplier.Active = request.Active;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = request.Active ? "SUPPLIER_ACTIVATED" : "SUPPLIER_DEACTIVATED", SubjectId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { supplier.Id, supplier.Active });
    });
        return protectedApi;
    }
}
