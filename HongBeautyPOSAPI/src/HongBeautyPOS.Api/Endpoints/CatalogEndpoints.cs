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

internal static class CatalogEndpoints
{
    internal static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder protectedApi)
    {
    protectedApi.MapGet("/products", async (ClaimsPrincipal principal, PosDbContext db, ProductCatalog catalog, string? q, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.read", ct)) return Results.Forbid();
        return Results.Ok(await catalog.ListAsync(q, ct));
    });
    protectedApi.MapGet("/attributes", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.read", ct) && !await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        return Results.Ok(await db.AttributeOptions.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Value)
            .Select(x => new AttributeOptionDto(x.Id, x.Name, x.Value)).ToListAsync(ct));
    });
    protectedApi.MapPost("/attributes", async (ClaimsPrincipal principal, PosDbContext db, CreateAttributeRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        var name = request.Name?.Trim();
        var values = request.Values?.Select(v => v?.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || values is null or { Length: 0 or > 30 } || values.Any(v => v!.Length > 100))
            return Results.BadRequest(new { error = "Tên thuộc tính hoặc giá trị không hợp lệ" });
        var nameKey = name.ToUpperInvariant();
        var valueKeys = values.Select(v => v!.ToUpperInvariant()).ToArray();
        var existing = await db.AttributeOptions.Where(x => x.NameKey == nameKey && valueKeys.Contains(x.ValueKey)).ToListAsync(ct);
        var added = new List<AttributeOption>();
        foreach (var value in values)
        {
            var key = value!.ToUpperInvariant();
            if (existing.Any(x => x.ValueKey == key)) continue;
            var option = new AttributeOption { Name = name, Value = value, NameKey = nameKey, ValueKey = key };
            db.AttributeOptions.Add(option);
            added.Add(option);
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "Giá trị thuộc tính vừa được thêm. Vui lòng tải lại." }); }
        return Results.Ok(existing.Concat(added).Select(x => new AttributeOptionDto(x.Id, x.Name, x.Value)));
    });
    protectedApi.MapPost("/products", async (ClaimsPrincipal principal, PosDbContext db, ProductCatalog catalog, CreateProductRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        if (request.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.Active, ct))
            return Results.BadRequest(new { error = "Danh mục không tồn tại" });
        if (!string.IsNullOrWhiteSpace(request.Sku) && await db.Products.AnyAsync(p => p.Sku == request.Sku.Trim(), ct))
            return Results.Conflict(new { error = "SKU đã tồn tại" });
        if (!string.IsNullOrWhiteSpace(request.Barcode) && await db.Products.AnyAsync(p => p.Barcode == request.Barcode.Trim(), ct))
            return Results.Conflict(new { error = "Mã vạch đã tồn tại" });
        var optionIds = request.AttributeOptionIds?.Distinct().ToArray() ?? [];
        if (optionIds.Length > 40 || await db.AttributeOptions.CountAsync(x => optionIds.Contains(x.Id), ct) != optionIds.Length)
            return Results.BadRequest(new { error = "Thuộc tính sản phẩm không hợp lệ" });
        try {
            var product = await catalog.CreateAsync(request, ct);
            db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "PRODUCT_CREATED", SubjectId = product.Id,
                DetailsJson = JsonSerializer.Serialize(new { product.Sku, product.Name, product.Barcode, product.Unit, product.SalePrice, product.CostPrice, product.ReorderLevel, product.CategoryId, hasImage = product.ImageData is not null, attributes = product.Attributes?.Select(a => $"{a.Name}: {a.Value}").ToArray() }) });
            await db.SaveChangesAsync(ct);
            return Results.Created("/api/v1/products", product);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    });
    protectedApi.MapPut("/products/{id:guid}", async (Guid id, ClaimsPrincipal principal, PosDbContext db, CreateProductRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        var sku = request.Sku?.Trim();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(sku) || sku.Length > 100 || string.IsNullOrWhiteSpace(name) || name.Length > 240 ||
            request.SalePrice < 0 || request.CostPrice < 0 || request.ReorderLevel < 0 || string.IsNullOrWhiteSpace(request.Unit) || request.Unit.Trim().Length > 40 || request.Barcode?.Trim().Length > 100)
            return Results.BadRequest(new { error = "Thông tin sản phẩm không hợp lệ" });
        if (request.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.Active, ct))
            return Results.BadRequest(new { error = "Danh mục không tồn tại" });
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();
        if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != id, ct))
            return Results.Conflict(new { error = "SKU đã tồn tại" });
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        if (barcode is not null && await db.Products.AnyAsync(p => p.Barcode == barcode && p.Id != id, ct))
            return Results.Conflict(new { error = "Mã vạch đã tồn tại" });
        string? imageData;
        try { imageData = request.ImageData is null ? product.ImageData : ProductImageRules.Normalize(request.ImageData); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        var optionIds = request.AttributeOptionIds?.Distinct().ToArray() ?? [];
        if (optionIds.Length > 40 || await db.AttributeOptions.CountAsync(x => optionIds.Contains(x.Id), ct) != optionIds.Length)
            return Results.BadRequest(new { error = "Thuộc tính sản phẩm không hợp lệ" });
        var oldProduct = new { product.Sku, product.Name, product.Barcode, product.Unit, product.SalePrice, product.CostPrice, product.ReorderLevel, product.CategoryId };
        product.Sku = sku;
        product.Barcode = barcode;
        product.ImageData = imageData;
        product.Name = name;
        product.Unit = request.Unit.Trim();
        product.ReorderLevel = request.ReorderLevel;
        product.SalePrice = request.SalePrice;
        product.CostPrice = request.CostPrice;
        product.CategoryId = request.CategoryId;
        var oldSelections = await db.ProductAttributeSelections.Where(x => x.ProductId == id).ToListAsync(ct);
        db.ProductAttributeSelections.RemoveRange(oldSelections.Where(x => !optionIds.Contains(x.OptionId)));
        var oldIds = oldSelections.Select(x => x.OptionId).ToHashSet();
        db.ProductAttributeSelections.AddRange(optionIds.Where(x => !oldIds.Contains(x))
            .Select(x => new ProductAttributeSelection { ProductId = id, OptionId = x }));
        product.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "PRODUCT_UPDATED", SubjectId = id, DetailsJson = JsonSerializer.Serialize(new { before = oldProduct, after = new { product.Sku, product.Name, product.Barcode, product.Unit, product.SalePrice, product.CostPrice, product.ReorderLevel, product.CategoryId } }) });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { product.Id, product.Sku, product.Barcode, product.Name, product.Unit, product.ReorderLevel, product.SalePrice, product.CostPrice, product.CategoryId, product.Active, AttributeOptionIds = optionIds });
    });
    protectedApi.MapPatch("/products/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();
        product.Active = request.Active;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = request.Active ? "PRODUCT_ACTIVATED" : "PRODUCT_DEACTIVATED", SubjectId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { product.Id, product.Active });
    });

    protectedApi.MapGet("/categories", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.read", ct)) return Results.Forbid();
        return Results.Ok(await db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.ParentId, c.Name, c.Active }).ToListAsync(ct));
    });
    protectedApi.MapPost("/categories", async (ClaimsPrincipal principal, PosDbContext db, CreateCategoryRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160) return Results.BadRequest(new { error = "Tên danh mục không hợp lệ" });
        if (request.ParentId.HasValue && !await db.Categories.AnyAsync(c => c.Id == request.ParentId && c.Active, ct))
            return Results.BadRequest(new { error = "Danh mục cha không tồn tại" });
        var category = new Category { Name = name, ParentId = request.ParentId };
        db.Categories.Add(category);
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "CATEGORY_CREATED", SubjectId = category.Id,
            DetailsJson = JsonSerializer.Serialize(new { category.Name, category.ParentId }) });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/categories/{category.Id}", new { category.Id, category.Name, category.ParentId, category.Active });
    });
    protectedApi.MapPut("/categories/{id:guid}", async (Guid id, ClaimsPrincipal principal, PosDbContext db, CreateCategoryRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160) return Results.BadRequest(new { error = "Tên danh mục không hợp lệ" });
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return Results.NotFound();
        if (request.ParentId.HasValue)
        {
            var parent = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.ParentId && c.Active, ct);
            if (parent is null) return Results.BadRequest(new { error = "Danh mục cha không tồn tại" });
            var ancestorIds = new HashSet<Guid>();
            while (parent is not null)
            {
                if (parent.Id == id || !ancestorIds.Add(parent.Id))
                    return Results.BadRequest(new { error = "Danh mục cha tạo vòng lặp" });
                parent = parent.ParentId.HasValue
                    ? await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == parent.ParentId, ct)
                    : null;
            }
        }
        var oldCategory = new { category.Name, category.ParentId };
        category.Name = name;
        category.ParentId = request.ParentId;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = "CATEGORY_UPDATED", SubjectId = id, DetailsJson = JsonSerializer.Serialize(new { before = oldCategory, after = new { category.Name, category.ParentId } }) });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { category.Id, category.Name, category.ParentId, category.Active });
    });
    protectedApi.MapPatch("/categories/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
    {
        if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return Results.NotFound();
        if (!request.Active && await db.Categories.AnyAsync(c => c.ParentId == id && c.Active, ct))
            return Results.Conflict(new { error = "Hãy ngừng sử dụng các danh mục con trước" });
        category.Active = request.Active;
        db.AuditEvents.Add(new AuditEvent { ActorId = principal.GetUserId(), Action = request.Active ? "CATEGORY_ACTIVATED" : "CATEGORY_DEACTIVATED", SubjectId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { category.Id, category.Active });
    });
        return protectedApi;
    }
}
