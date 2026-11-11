using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Api.Security;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Startup;

internal static class PipelineExtensions
{
    public static WebApplication UsePosPipeline(this WebApplication app, AppSettings settings)
    {
        if (settings.UseForwardedHeaders) app.UseForwardedHeaders();
        app.UseSecurityHeaders();
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Lỗi máy chủ" });
        }));
        app.UseCors();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        return app;
    }

    public static WebApplication MapHealth(this WebApplication app)
    {
        app.MapGet("/health", async (PosDbContext db) =>
        {
            if (!await db.Database.CanConnectAsync()) return Results.StatusCode(503);
            return (await db.Database.GetPendingMigrationsAsync()).Any()
                ? Results.StatusCode(503)
                : Results.Ok(new { status = "ok" });
        });
        return app;
    }

    /// <summary>Serves the Angular build (wwwroot) for any non-API path when it is present.</summary>
    public static WebApplication MapSpaFallback(this WebApplication app)
    {
        if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
            app.MapFallbackToFile("index.html");
        return app;
    }
}
