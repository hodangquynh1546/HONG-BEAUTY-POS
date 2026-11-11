using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Startup;

internal static class StartupTasks
{
    /// <summary>Runs one-shot operator commands (--migrate, --bootstrap-owner, --recover-owner-password). Returns true when handled.</summary>
    public static async Task<bool> TryRunCommandAsync(WebApplication app, string[] args)
    {
        string? command = args.FirstOrDefault(a => a is "--migrate" or "--bootstrap-owner" or "--recover-owner-password");
        if (command is null) return false;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var configuration = app.Configuration;
        switch (command)
        {
            case "--migrate":
                await db.Database.MigrateAsync();
                Console.WriteLine("Database migrations applied");
                break;
            case "--bootstrap-owner":
                await OwnerAccount.BootstrapAsync(db, configuration);
                Console.WriteLine("Owner account created");
                break;
            default:
                await OwnerAccount.RecoverPasswordAsync(db, configuration);
                Console.WriteLine("Owner password recovered; previous sessions are invalid");
                break;
        }
        return true;
    }

    public static async Task RunOnStartAsync(WebApplication app, AppSettings settings)
    {
        if (settings.RunMigrationsOnStart)
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
        }
        if (settings.BootstrapOwnerOnStart)
        {
            using var scope = app.Services.CreateScope();
            await OwnerAccount.BootstrapOnStartAsync(scope.ServiceProvider.GetRequiredService<PosDbContext>(), app.Configuration, app.Logger);
        }
    }
}
