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

internal static class CustomersEndpoints
{
    internal static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/customer-groups", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.read", ct)) return Results.Forbid();
        return Results.Ok(await db.CustomerGroups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(ct));
    });
    protectedApi.MapPost("/customer-groups", async (ClaimsPrincipal principal, PosDbContext db, CreateCustomerGroupRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160) return Results.BadRequest(new { error = "Tên nhóm khách hàng không hợp lệ" });
        if (await db.CustomerGroups.AnyAsync(g => g.Name.ToLower() == name.ToLower(), ct)) return Results.Conflict(new { error = "Nhóm khách hàng đã tồn tại" });
        var group = new CustomerGroup { Name = name };
        db.CustomerGroups.Add(group);
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "CUSTOMER_GROUP_CREATED", SubjectId = group.Id });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/customer-groups/{group.Id}", group);
    });
    protectedApi.MapGet("/customers", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.read", ct)) return Results.Forbid();
        return Results.Ok(await db.Customers.AsNoTracking().OrderBy(c => c.FullName)
            .Select(c => new { c.Id, c.FullName, c.Phone, c.Phone2, c.Gender, c.Address, c.Contact, c.Notes, c.AvatarData, c.GroupId, c.Email, c.Active }).Take(200).ToListAsync(ct));
    });
    protectedApi.MapGet("/customers/search", async (ClaimsPrincipal principal, PosDbContext db, string? q, int? skip, int? take, bool? activeOnly, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.read", ct)) return Results.Forbid();
        var offset = skip ?? 0;
        var pageSize = take ?? 50;
        var term = q?.Trim();
        if (offset < 0 || pageSize is < 1 or > 100 || term?.Length > 100)
            return Results.BadRequest(new { error = "Bộ lọc khách hàng không hợp lệ" });
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (activeOnly == true) query = query.Where(c => c.Active);
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.ILike(c.FullName, pattern, "\\") ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, pattern, "\\")) ||
                (c.Email != null && EF.Functions.ILike(c.Email, pattern, "\\")) ||
                (c.Phone2 != null && EF.Functions.ILike(c.Phone2, pattern, "\\")));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.FullName).ThenBy(c => c.Id)
            .Skip(offset).Take(pageSize)
            .Select(c => new { c.Id, c.FullName, c.Phone, c.Phone2, c.Gender, c.Address, c.Contact, c.Notes, c.AvatarData, c.GroupId, c.Email, c.Active }).ToListAsync(ct);
        return Results.Ok(new { items, total });
    });
    protectedApi.MapPost("/customers", async (ClaimsPrincipal principal, PosDbContext db, CreateCustomerRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
        var name = request.FullName?.Trim();
        var phone = request.Phone?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || phone?.Length > 40 || email?.Length > 320 || !ValidCustomerDetails(request))
            return Results.BadRequest(new { error = "Thông tin khách hàng không hợp lệ" });
        if (request.GroupId.HasValue && !await db.CustomerGroups.AnyAsync(g => g.Id == request.GroupId && g.Active, ct)) return Results.BadRequest(new { error = "Nhóm khách hàng không hợp lệ" });
        string? avatar;
        try { avatar = ProductImageRules.Normalize(request.AvatarData); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        var customer = new Customer { FullName = name, Phone = phone, Email = email, Phone2 = request.Phone2?.Trim(), Gender = request.Gender, Address = request.Address?.Trim(), Contact = request.Contact?.Trim(), Notes = request.Notes?.Trim(), GroupId = request.GroupId, AvatarData = avatar };
        db.Customers.Add(customer);
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "CUSTOMER_CREATED", SubjectId = customer.Id,
            DetailsJson = JsonSerializer.Serialize(new { customer.FullName, customer.Phone, customer.Phone2, customer.Email, customer.Gender, customer.Address, customer.Contact, customer.Notes, customer.GroupId, hasAvatar = avatar is not null }) });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/customers/{customer.Id}", customer);
    });
    protectedApi.MapPut("/customers/{id:guid}", async (Guid id, ClaimsPrincipal principal, PosDbContext db, CreateCustomerRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
        var name = request.FullName?.Trim();
        var phone = request.Phone?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || phone?.Length > 40 || email?.Length > 320 || !ValidCustomerDetails(request))
            return Results.BadRequest(new { error = "Thông tin khách hàng không hợp lệ" });
        if (request.GroupId.HasValue && !await db.CustomerGroups.AnyAsync(g => g.Id == request.GroupId && g.Active, ct)) return Results.BadRequest(new { error = "Nhóm khách hàng không hợp lệ" });
        string? avatar;
        try { avatar = ProductImageRules.Normalize(request.AvatarData); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return Results.NotFound();
        var oldCustomer = new { customer.FullName, customer.Phone, customer.Phone2, customer.Email, customer.Gender, customer.Address, customer.Contact, customer.Notes, customer.GroupId, hasAvatar = customer.AvatarData is not null };
        customer.FullName = name;
        customer.Phone = phone;
        customer.Email = email;
        customer.Phone2 = request.Phone2?.Trim();
        customer.Gender = request.Gender;
        customer.Address = request.Address?.Trim();
        customer.Contact = request.Contact?.Trim();
        customer.Notes = request.Notes?.Trim();
        customer.GroupId = request.GroupId;
        customer.AvatarData = avatar;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "CUSTOMER_UPDATED", SubjectId = id, DetailsJson = JsonSerializer.Serialize(new { before = oldCustomer, after = new { customer.FullName, customer.Phone, customer.Phone2, customer.Email, customer.Gender, customer.Address, customer.Contact, customer.Notes, customer.GroupId, hasAvatar = avatar is not null } }) });
        await db.SaveChangesAsync(ct);
        return Results.Ok(customer);
    });
    protectedApi.MapPatch("/customers/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return Results.NotFound();
        customer.Active = request.Active;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = request.Active ? "CUSTOMER_ACTIVATED" : "CUSTOMER_DEACTIVATED", SubjectId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { customer.Id, customer.Active });
    });
        return protectedApi;
    }

    private static bool ValidCustomerDetails(CreateCustomerRequest request) =>
        (request.Phone2 is null || request.Phone2.Length <= 40) &&
        (request.Address is null || request.Address.Length <= 500) &&
        (request.Contact is null || request.Contact.Length <= 200) &&
        (request.Notes is null || request.Notes.Length <= 2000) &&
        (request.Gender is null or "Male" or "Female" or "Other");
}
