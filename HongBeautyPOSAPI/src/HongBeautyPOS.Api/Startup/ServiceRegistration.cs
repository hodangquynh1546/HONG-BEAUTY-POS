using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Api.Security;
using HongBeautyPOS.Application;
using HongBeautyPOS.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HongBeautyPOS.Api.Startup;

internal static class ServiceRegistration
{
    public static IServiceCollection AddPosServices(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddDbContext<PosDbContext>(options => options.UseNpgsql(settings.ConnectionString));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ProductCatalog>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<InventoryService>();
        services.AddScoped<IStocktakeRepository, StocktakeRepository>();
        services.AddScoped<StocktakeService>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<PurchaseService>();
        services.AddScoped<ISalesRepository, SalesRepository>();
        services.AddScoped<SalesService>();
        services.AddScoped<IReturnRepository, ReturnRepository>();
        services.AddScoped<ReturnService>();
        services.AddScoped<IReportsRepository, ReportsRepository>();
        services.AddScoped<ReportService>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(settings.CorsOrigins).AllowAnyHeader().AllowAnyMethod()));

        AddAuthentication(services, settings);
        AddRateLimiting(services, settings);

        if (settings.UseForwardedHeaders)
        {
            // Behind a reverse proxy (Render, nginx) the socket peer is the proxy. Without this every client
            // shares one login rate-limit bucket. ForwardLimit=1 trusts only the entry the proxy itself appended.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardLimit = 1;
            });
        }
        return services;
    }

    private static void AddAuthentication(IServiceCollection services, AppSettings settings)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = AppSettings.JwtIssuer,
                ValidateAudience = true, ValidAudience = AppSettings.JwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(settings.JwtSecret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                // Reject tokens of deactivated users and tokens issued before the last password change.
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
                    var version = user is null ? null : JwtTokenService.PasswordVersion(user.PasswordHash);
                    if (version is null || !string.Equals(context.Principal?.FindFirstValue("pwdv"), version, StringComparison.Ordinal))
                        context.Fail("Session expired");
                }
            };
        });
        services.AddAuthorization();
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });

    private static void AddRateLimiting(IServiceCollection services, AppSettings settings) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Login, context => PerIp(context, settings.LoginRateLimitPerMinute));
            // Token renewal is routine traffic (every access-token lifetime, per device) so it gets a roomier bucket.
            options.AddPolicy(RateLimitPolicies.Session, context => PerIp(context, Math.Max(120, settings.LoginRateLimitPerMinute)));
        });
}

internal static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Session = "session";
}
