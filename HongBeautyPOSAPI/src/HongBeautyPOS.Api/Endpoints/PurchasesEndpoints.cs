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

internal static class PurchasesEndpoints
{
    internal static RouteGroupBuilder MapPurchaseEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/purchases", async (ClaimsPrincipal principal, PosDbContext db, PurchaseService purchases, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "purchases.read", ct)) return Results.Forbid();
        return Results.Ok(await purchases.ListAsync(ct));
    });
    protectedApi.MapPost("/purchases", async (ClaimsPrincipal principal, PosDbContext db, PurchaseService purchases, CreatePurchaseRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "purchases.write", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        if (request.SupplierId.HasValue && !await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.Active, ct))
            return Results.BadRequest(new { error = "Nhà cung cấp không tồn tại" });
        try
        {
            var receipt = await purchases.CreateAsync(request, actorId, ct);
            return Results.Created($"/api/v1/purchases/{receipt.Id}", receipt);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    });
    protectedApi.MapPost("/purchases/{id:guid}/complete", async (ClaimsPrincipal principal, PosDbContext db, PurchaseService purchases, Guid id, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "purchases.write", ct)) return Results.Forbid();
        if (principal.TryGetUserId() is not { } actorId) return Results.Unauthorized();
        var result = await purchases.CompleteAsync(id, actorId, ct);
        return result is null ? Results.Conflict(new { error = "Phiếu nhập không tồn tại hoặc đã xử lý" }) : Results.Ok(result);
    });
        return protectedApi;
    }
}
