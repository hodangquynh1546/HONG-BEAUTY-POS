using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HongBeautyPOS.Application;
using HongBeautyPOS.Domain;
using HongBeautyPOS.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connection = DatabaseConnection.Normalize(Environment.GetEnvironmentVariable("DATABASE_URL") ?? string.Empty);
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET is required");
if (jwtSecret.Length < 32 || jwtSecret.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("JWT_SECRET must be a strong random value of at least 32 characters");

builder.Services.AddDbContext<PosDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ProductCatalog>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<IStocktakeRepository, StocktakeRepository>();
builder.Services.AddScoped<StocktakeService>();
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepository>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<ISalesRepository, SalesRepository>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<IReturnRepository, ReturnRepository>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<IReportsRepository, ReportsRepository>();
builder.Services.AddScoped<ReportService>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "HongBeautyPOS",
        ValidateAudience = true, ValidAudience = "HongBeautyPOSUI",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
            {
                context.Fail("Invalid user");
                return;
            }
            var db = context.HttpContext.RequestServices.GetRequiredService<PosDbContext>();
            var user = await db.Users.AsNoTracking().Where(u => u.Id == id && u.Active)
                .Select(u => new { u.PasswordHash }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            var version = user is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)));
            if (version is null || !string.Equals(context.Principal?.FindFirstValue("pwdv"), version, StringComparison.Ordinal))
                context.Fail("Session expired");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var origins = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "Lỗi máy chủ" });
}));
app.UseCors();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", async (PosDbContext db) =>
{
    if (!await db.Database.CanConnectAsync()) return Results.StatusCode(503);
    return (await db.Database.GetPendingMigrationsAsync()).Any()
        ? Results.StatusCode(503)
        : Results.Ok(new { status = "ok" });
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
    Console.WriteLine("Database migrations applied");
    return;
}

if (args.Contains("--bootstrap-owner", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    await db.Database.MigrateAsync();
    if (await db.Users.AnyAsync(u => u.Role == UserRole.Owner)) throw new InvalidOperationException("Owner already exists");
    var email = Environment.GetEnvironmentVariable("OWNER_EMAIL")?.Trim().ToLowerInvariant();
    var name = Environment.GetEnvironmentVariable("OWNER_NAME")?.Trim();
    var password = Environment.GetEnvironmentVariable("OWNER_PASSWORD");
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || password is null || password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) || password.Length < 14 ||
        !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
        throw new InvalidOperationException("OWNER_EMAIL, OWNER_NAME and a strong OWNER_PASSWORD are required");
    db.Users.Add(new User { Email = email, FullName = name, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = UserRole.Owner });
    await db.SaveChangesAsync();
    Console.WriteLine("Owner account created");
    return;
}

if (string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_START"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
}

if (string.Equals(Environment.GetEnvironmentVariable("BOOTSTRAP_OWNER_ON_START"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    if (!await db.Users.AnyAsync(u => u.Role == UserRole.Owner))
    {
        var email = Environment.GetEnvironmentVariable("OWNER_EMAIL")?.Trim().ToLowerInvariant();
        var name = Environment.GetEnvironmentVariable("OWNER_NAME")?.Trim();
        var password = Environment.GetEnvironmentVariable("OWNER_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || password is null || password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) || password.Length < 14 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
            app.Logger.LogWarning("Owner setup is pending: set valid OWNER_EMAIL, OWNER_NAME and OWNER_PASSWORD in the service environment, then redeploy.");
        else
        {
            db.Users.Add(new User { Email = email, FullName = name, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = UserRole.Owner });
            await db.SaveChangesAsync();
        }
    }
}

var api = app.MapGroup("/api/v1");
api.MapPost("/auth/login", async (LoginRequest request, PosDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email && u.Active);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();
    var token = new JwtSecurityToken(
        issuer: "HongBeautyPOS", audience: "HongBeautyPOSUI",
        claims: [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role.ToString()), new Claim("pwdv", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash))))],
        notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), role = user.Role.ToString() });
}).RequireRateLimiting("login");

var protectedApi = api.MapGroup(string.Empty).RequireAuthorization();
protectedApi.MapGet("/me", async (ClaimsPrincipal principal, PosDbContext db) =>
{
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return Results.Unauthorized();
    var user = await db.Users.AsNoTracking().Where(u => u.Id == id && u.Active).SingleOrDefaultAsync();
    if (user is null) return Results.Unauthorized();
    var permissions = user.Role == UserRole.Owner ? Array.Empty<string>() :
        await db.EmployeePermissions.Where(p => p.UserId == id).Select(p => p.Permission).ToArrayAsync();
    return Results.Ok(new { user.Id, user.Email, user.FullName, Role = user.Role.ToString(), Permissions = permissions });
});
protectedApi.MapGet("/products", async (ClaimsPrincipal principal, PosDbContext db, ProductCatalog catalog, string? q, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "products.read", ct)) return Results.Forbid();
    return Results.Ok(await catalog.ListAsync(q, ct));
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
    try { return Results.Created("/api/v1/products", await catalog.CreateAsync(request, ct)); }
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
    product.Sku = sku;
    product.Barcode = barcode;
    product.Name = name;
    product.Unit = request.Unit.Trim();
    product.ReorderLevel = request.ReorderLevel;
    product.SalePrice = request.SalePrice;
    product.CostPrice = request.CostPrice;
    product.CategoryId = request.CategoryId;
    product.UpdatedAt = DateTimeOffset.UtcNow;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = "PRODUCT_UPDATED", SubjectId = id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { product.Id, product.Sku, product.Barcode, product.Name, product.Unit, product.ReorderLevel, product.SalePrice, product.CostPrice, product.CategoryId, product.Active });
});
protectedApi.MapPatch("/products/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "products.write", ct)) return Results.Forbid();
    var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
    if (product is null) return Results.NotFound();
    product.Active = request.Active;
    product.UpdatedAt = DateTimeOffset.UtcNow;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = request.Active ? "PRODUCT_ACTIVATED" : "PRODUCT_DEACTIVATED", SubjectId = id });
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
    category.Name = name;
    category.ParentId = request.ParentId;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = "CATEGORY_UPDATED", SubjectId = id });
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
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = request.Active ? "CATEGORY_ACTIVATED" : "CATEGORY_DEACTIVATED", SubjectId = id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { category.Id, category.Active });
});

protectedApi.MapGet("/customers", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "customers.read", ct)) return Results.Forbid();
    return Results.Ok(await db.Customers.AsNoTracking().OrderBy(c => c.FullName)
        .Select(c => new { c.Id, c.FullName, c.Phone, c.Email, c.Active }).Take(200).ToListAsync(ct));
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
            (c.Email != null && EF.Functions.ILike(c.Email, pattern, "\\")));
    }
    var total = await query.CountAsync(ct);
    var items = await query.OrderBy(c => c.FullName).ThenBy(c => c.Id)
        .Skip(offset).Take(pageSize)
        .Select(c => new { c.Id, c.FullName, c.Phone, c.Email, c.Active }).ToListAsync(ct);
    return Results.Ok(new { items, total });
});
protectedApi.MapPost("/customers", async (ClaimsPrincipal principal, PosDbContext db, CreateCustomerRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
    var name = request.FullName?.Trim();
    var phone = request.Phone?.Trim();
    var email = request.Email?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || phone?.Length > 40 || email?.Length > 320)
        return Results.BadRequest(new { error = "Thông tin khách hàng không hợp lệ" });
    var customer = new Customer { FullName = name, Phone = phone, Email = email };
    db.Customers.Add(customer);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/customers/{customer.Id}", new { customer.Id, customer.FullName, customer.Phone, customer.Email });
});
protectedApi.MapPut("/customers/{id:guid}", async (Guid id, ClaimsPrincipal principal, PosDbContext db, CreateCustomerRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
    var name = request.FullName?.Trim();
    var phone = request.Phone?.Trim();
    var email = request.Email?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || phone?.Length > 40 || email?.Length > 320)
        return Results.BadRequest(new { error = "Thông tin khách hàng không hợp lệ" });
    var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
    if (customer is null) return Results.NotFound();
    customer.FullName = name;
    customer.Phone = phone;
    customer.Email = email;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = "CUSTOMER_UPDATED", SubjectId = id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { customer.Id, customer.FullName, customer.Phone, customer.Email, customer.Active });
});
protectedApi.MapPatch("/customers/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "customers.write", ct)) return Results.Forbid();
    var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
    if (customer is null) return Results.NotFound();
    customer.Active = request.Active;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = request.Active ? "CUSTOMER_ACTIVATED" : "CUSTOMER_DEACTIVATED", SubjectId = id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { customer.Id, customer.Active });
});

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
    supplier.Code = code;
    supplier.Name = name;
    supplier.Phone = phone;
    supplier.Email = email;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = "SUPPLIER_UPDATED", SubjectId = id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { supplier.Id, supplier.Code, supplier.Name, supplier.Phone, supplier.Email, supplier.Active });
});
protectedApi.MapPatch("/suppliers/{id:guid}/status", async (Guid id, ClaimsPrincipal principal, PosDbContext db, ActiveStatusRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "suppliers.write", ct)) return Results.Forbid();
    var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct);
    if (supplier is null) return Results.NotFound();
    supplier.Active = request.Active;
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = request.Active ? "SUPPLIER_ACTIVATED" : "SUPPLIER_DEACTIVATED", SubjectId = id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { supplier.Id, supplier.Active });
});

protectedApi.MapGet("/stock", async (ClaimsPrincipal principal, PosDbContext db, InventoryService inventory, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "inventory.read", ct)) return Results.Forbid();
    return Results.Ok(await inventory.ListAsync(ct));
});
protectedApi.MapPost("/stock/adjustments", async (ClaimsPrincipal principal, PosDbContext db, InventoryService inventory, StockAdjustmentRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "inventory.adjust", ct)) return Results.Forbid();
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
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
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
    try
    {
        var session = await stocktakes.CreateAsync(actorId, ct);
        return Results.Created($"/api/v1/stocktakes/{session.Id}", session);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
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
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
    try
    {
        var session = await stocktakes.CompleteAsync(id, actorId, ct);
        return session is null ? Results.Conflict(new { error = "Phiên kiểm kho đã hoàn tất hoặc không tồn tại" }) : Results.Ok(session);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

protectedApi.MapGet("/purchases", async (ClaimsPrincipal principal, PosDbContext db, PurchaseService purchases, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "purchases.read", ct)) return Results.Forbid();
    return Results.Ok(await purchases.ListAsync(ct));
});
protectedApi.MapPost("/purchases", async (ClaimsPrincipal principal, PosDbContext db, PurchaseService purchases, CreatePurchaseRequest request, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "purchases.write", ct)) return Results.Forbid();
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
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
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
    var result = await purchases.CompleteAsync(id, actorId, ct);
    return result is null ? Results.Conflict(new { error = "Phiếu nhập không tồn tại hoặc đã xử lý" }) : Results.Ok(result);
});

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
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
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
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
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
        .Select(i => new { i.SkuSnapshot, i.NameSnapshot, i.Quantity, i.UnitPrice, LineTotal = i.Quantity * i.UnitPrice }).ToListAsync(ct);
    var payments = await db.Payments.AsNoTracking().Where(p => p.OrderId == id).OrderBy(p => p.ReceivedAt)
        .Select(p => new { p.Method, p.Amount, p.ReceivedAt }).ToListAsync(ct);
    return Results.Ok(new { order.Id, order.OrderNo, order.CreatedAt, Status = order.Status.ToString(),
        CustomerName = customerName ?? "Khách lẻ", SellerName = sellerName ?? "—", order.Subtotal, order.Discount,
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
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorId)) return Results.Unauthorized();
    try
    {
        var result = await returns.CreateAsync(id, request, actorId, ct);
        return Results.Created($"/api/v1/orders/{id}/returns", result);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

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
        CreatedBy = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!)
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

protectedApi.MapGet("/reports/summary", async (ClaimsPrincipal principal, PosDbContext db, ReportService reports, DateOnly? from, DateOnly? to, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "reports.read", ct)) return Results.Forbid();
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    try { return Results.Ok(await reports.SummaryAsync(from ?? today, to ?? today, ct)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

protectedApi.MapGet("/reports/payment-methods", async (ClaimsPrincipal principal, PosDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
{
    if (!await HasPermission(principal, db, "reports.read", ct)) return Results.Forbid();
    var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
    var startDate = from ?? today;
    var endDate = to ?? today;
    if (endDate < startDate || endDate.DayNumber - startDate.DayNumber > 366)
        return Results.BadRequest(new { error = "Khoảng báo cáo phải từ 1 đến 367 ngày." });
    var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startDate.ToDateTime(TimeOnly.MinValue), zone));
    var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
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
    var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
    var startDate = from ?? today;
    var endDate = to ?? today;
    if (endDate < startDate || endDate.DayNumber - startDate.DayNumber > 366)
        return Results.BadRequest(new { error = "Khoảng báo cáo phải từ 1 đến 367 ngày." });
    var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startDate.ToDateTime(TimeOnly.MinValue), zone));
    var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
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

protectedApi.MapGet("/employees", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
{
    if (!await IsOwner(principal, db, ct)) return Results.Forbid();
    var employees = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Employee)
        .Include(u => u.Permissions).OrderBy(u => u.FullName).ToListAsync(ct);
    return Results.Ok(employees.Select(u => new
    {
        u.Id, u.Email, u.FullName, u.Active,
        Permissions = u.Permissions.Select(p => p.Permission).OrderBy(p => p).ToArray()
    }));
});

protectedApi.MapGet("/permissions", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
    await IsOwner(principal, db, ct) ? Results.Ok(PermissionRules.Allowed.OrderBy(p => p)) : Results.Forbid());

protectedApi.MapPost("/employees", async (ClaimsPrincipal principal, PosDbContext db, CreateEmployeeRequest request, CancellationToken ct) =>
{
    if (!await IsOwner(principal, db, ct)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) ||
        request.Password is null || request.Password.Length < 12 || !PermissionRules.AreValid(request.Permissions))
        return Results.BadRequest(new { error = "Thông tin nhân viên hoặc quyền không hợp lệ" });
    var ownerId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    var user = new User
    {
        Email = request.Email.Trim().ToLowerInvariant(), FullName = request.FullName.Trim(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), Role = UserRole.Employee
    };
    foreach (var permission in request.Permissions!.Distinct(StringComparer.Ordinal))
        user.Permissions.Add(new EmployeePermission { Permission = permission, GrantedBy = ownerId });
    db.Users.Add(user);
    db.AuditEvents.Add(new AuditEvent { ActorId = ownerId, Action = "EMPLOYEE_CREATED", SubjectId = user.Id });
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/employees/{user.Id}", new { user.Id, user.Email, user.FullName });
});

protectedApi.MapPut("/employees/{id:guid}/permissions", async (ClaimsPrincipal principal, PosDbContext db, Guid id, ReplacePermissionsRequest request, CancellationToken ct) =>
{
    if (!await IsOwner(principal, db, ct)) return Results.Forbid();
    if (!PermissionRules.AreValid(request.Permissions)) return Results.BadRequest(new { error = "Quyền không hợp lệ" });
    var employee = await db.Users.Include(u => u.Permissions).SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Employee, ct);
    if (employee is null) return Results.NotFound();
    var ownerId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    db.EmployeePermissions.RemoveRange(employee.Permissions);
    employee.Permissions.Clear();
    await db.SaveChangesAsync(ct);
    foreach (var permission in request.Permissions!.Distinct(StringComparer.Ordinal))
        employee.Permissions.Add(new EmployeePermission { Permission = permission, GrantedBy = ownerId });
    db.AuditEvents.Add(new AuditEvent { ActorId = ownerId, Action = "EMPLOYEE_PERMISSIONS_REPLACED", SubjectId = employee.Id });
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
    var ownerId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    db.AuditEvents.Add(new AuditEvent { ActorId = ownerId, Action = request.Active ? "EMPLOYEE_ACTIVATED" : "EMPLOYEE_DEACTIVATED", SubjectId = employee.Id });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { employee.Id, employee.Active });
});

protectedApi.MapPut("/employees/{id:guid}/password", async (ClaimsPrincipal principal, PosDbContext db, Guid id, ResetEmployeePasswordRequest request, CancellationToken ct) =>
{
    if (!await IsOwner(principal, db, ct)) return Results.Forbid();
    var password = request.Password;
    if (string.IsNullOrEmpty(password) || password.Length < 14 || password.Length > 128 ||
        !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
        return Results.BadRequest(new { error = "Mật khẩu mới phải dài 14–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt" });
    var employee = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Employee, ct);
    if (employee is null) return Results.NotFound();
    employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
    db.AuditEvents.Add(new AuditEvent { ActorId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), Action = "EMPLOYEE_PASSWORD_RESET", SubjectId = employee.Id });
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

protectedApi.MapGet("/audit-events", async (ClaimsPrincipal principal, PosDbContext db, CancellationToken ct) =>
{
    if (!await IsOwner(principal, db, ct)) return Results.Forbid();
    var events = await db.AuditEvents.AsNoTracking().OrderByDescending(e => e.CreatedAt).Take(100).ToListAsync(ct);
    var ids = events.SelectMany(e => new[] { e.ActorId, e.SubjectId }).Distinct().ToArray();
    var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
    var categories = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    var customers = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
    var suppliers = await db.Suppliers.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    var orders = await db.SalesOrders.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
    var returns = await (from r in db.SalesReturns.AsNoTracking() join o in db.SalesOrders.AsNoTracking() on r.OrderId equals o.Id
        where ids.Contains(r.Id) select new { r.Id, o.OrderNo }).ToDictionaryAsync(x => x.Id, x => x.OrderNo, ct);
    var entries = await db.CashEntries.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Description, ct);
    return Results.Ok(events.Select(e => new { e.Id, e.ActorId, e.SubjectId, e.Action, e.CreatedAt,
        ActorName = users.GetValueOrDefault(e.ActorId, "Người dùng đã ngừng hoạt động"),
        SubjectName = e.Action.StartsWith("EMPLOYEE_", StringComparison.Ordinal) ? users.GetValueOrDefault(e.SubjectId, "Nhân viên") :
            e.Action.StartsWith("PRODUCT_", StringComparison.Ordinal) ? products.GetValueOrDefault(e.SubjectId, "Sản phẩm") :
            e.Action.StartsWith("CATEGORY_", StringComparison.Ordinal) ? categories.GetValueOrDefault(e.SubjectId, "Danh mục") :
            e.Action.StartsWith("CUSTOMER_", StringComparison.Ordinal) ? customers.GetValueOrDefault(e.SubjectId, "Khách hàng") :
            e.Action.StartsWith("SUPPLIER_", StringComparison.Ordinal) ? suppliers.GetValueOrDefault(e.SubjectId, "Nhà cung cấp") :
            e.Action.StartsWith("SALE_RETURN_", StringComparison.Ordinal) ? "Đơn " + returns.GetValueOrDefault(e.SubjectId, "đã trả hàng") :
            e.Action.StartsWith("SALE_", StringComparison.Ordinal) ? "Đơn " + orders.GetValueOrDefault(e.SubjectId, "bán hàng") :
            e.Action.StartsWith("CASH_", StringComparison.Ordinal) ? entries.GetValueOrDefault(e.SubjectId, "Phiếu thu chi") : "Giao dịch" }));
});

if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
    app.MapFallbackToFile("index.html");

app.Run();

static async Task<bool> HasPermission(ClaimsPrincipal principal, PosDbContext db, string permission, CancellationToken ct)
{
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return false;
    var user = await db.Users.AsNoTracking().Where(u => u.Id == id && u.Active)
        .Select(u => new { u.Role }).SingleOrDefaultAsync(ct);
    if (user is null) return false;
    return user.Role == UserRole.Owner || await db.EmployeePermissions.AnyAsync(p => p.UserId == id && p.Permission == permission, ct);
}

static async Task<bool> IsOwner(ClaimsPrincipal principal, PosDbContext db, CancellationToken ct)
{
    if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return false;
    return await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Active && u.Role == UserRole.Owner, ct);
}

sealed record LoginRequest(string Email, string Password);
sealed record CreateEmployeeRequest(string Email, string FullName, string Password, string[]? Permissions);
sealed record ReplacePermissionsRequest(string[]? Permissions);
sealed record SetEmployeeStatusRequest(bool Active);
sealed record ResetEmployeePasswordRequest(string Password);
sealed record ActiveStatusRequest(bool Active);
sealed record CreateCategoryRequest(string Name, Guid? ParentId);
sealed record CreateCustomerRequest(string FullName, string? Phone, string? Email);
sealed record CreateSupplierRequest(string Code, string Name, string? Phone, string? Email);
sealed record CountStocktakeItemRequest(decimal ActualQuantity);
sealed record CreateCashEntryRequest(Guid IdempotencyKey, CashEntryKind Kind, PaymentMethod Method, decimal Amount, string Description);
