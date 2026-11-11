
namespace HongBeautyPOS.Api.Configuration;

/// <summary>
/// Typed view of the environment configuration. Every value is read through
/// <see cref="IConfiguration"/>, so it can come from environment variables
/// (production), user-secrets or appsettings files (local development).
/// </summary>
internal sealed record AppSettings(
    string ConnectionString,
    string JwtSecret,
    string[] CorsOrigins,
    bool RunMigrationsOnStart,
    bool BootstrapOwnerOnStart,
    bool UseForwardedHeaders)
{
    public const string JwtIssuer = "HongBeautyPOS";
    public const string JwtAudience = "HongBeautyPOSUI";

    public static AppSettings Load(IConfiguration configuration)
    {
        var secret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JWT_SECRET is required");
        if (secret.Length < 32 || secret.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("JWT_SECRET must be a strong random value of at least 32 characters");

        return new AppSettings(
            ConnectionString: HongBeautyPOS.Infrastructure.DatabaseConnection.Normalize(configuration["DATABASE_URL"] ?? string.Empty),
            JwtSecret: secret,
            CorsOrigins: (configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            RunMigrationsOnStart: Flag(configuration, "RUN_MIGRATIONS_ON_START"),
            BootstrapOwnerOnStart: Flag(configuration, "BOOTSTRAP_OWNER_ON_START"),
            UseForwardedHeaders: Flag(configuration, "FORWARDED_HEADERS_ENABLED"));
    }

    private static bool Flag(IConfiguration configuration, string key) =>
        string.Equals(configuration[key], "true", StringComparison.OrdinalIgnoreCase);
}
