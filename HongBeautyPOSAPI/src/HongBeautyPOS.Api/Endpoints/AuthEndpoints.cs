using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Api.Contracts;
using HongBeautyPOS.Api.Security;
using HongBeautyPOS.Api.Startup;
using HongBeautyPOS.Domain;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Endpoints;

internal static class AuthEndpoints
{
    // Verified when the email is unknown so response time does not reveal which accounts exist.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-equalizer-" + Guid.NewGuid());

    internal static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", async (LoginRequest request, HttpContext http, PosDbContext db, JwtTokenService tokens,
            RefreshTokenService refresh, AppSettings settings, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email && u.Active, ct);
            var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
            if (user is null || !passwordMatches) return Results.Unauthorized();
            return Session(http, settings, user, tokens, await refresh.IssueAsync(user, ct));
        }).RequireRateLimiting(RateLimitPolicies.Login);

        api.MapPost("/auth/refresh", async (RefreshRequest request, HttpContext http, JwtTokenService tokens,
            RefreshTokenService refresh, AppSettings settings, CancellationToken ct) =>
        {
            if (!TryGetRefreshToken(http, request, out var token)) return Reject(http);
            var result = await refresh.RotateAsync(token, ct);
            return result is null ? Reject(http) : Session(http, settings, result.User, tokens, result.RefreshToken);
        }).RequireRateLimiting(RateLimitPolicies.Session);

        api.MapPost("/auth/logout", async (RefreshRequest request, HttpContext http, RefreshTokenService refresh, CancellationToken ct) =>
        {
            if (TryGetRefreshToken(http, request, out var token)) await refresh.RevokeAsync(token, ct);
            ClearCookie(http);
            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.Session);
        return api;
    }

    /// <summary>
    /// Browsers get the refresh token only as an HttpOnly cookie (script cannot read it). Native apps run on a
    /// different origin where cookies are unreliable, so they opt in with <c>X-Client: native</c> and keep it themselves.
    /// </summary>
    private static bool IsNativeClient(HttpRequest request) =>
        string.Equals(request.Headers["X-Client"], "native", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetRefreshToken(HttpContext http, RefreshRequest request, out string? token)
    {
        if (IsNativeClient(http.Request)) { token = request.RefreshToken; return !string.IsNullOrWhiteSpace(token); }
        // Cookie mode: require a custom header, which a cross-site form or image request cannot send (CSRF defence
        // on top of SameSite=Strict).
        token = null;
        if (!http.Request.Headers.ContainsKey(RefreshCookie.CsrfHeader)) return false;
        token = http.Request.Cookies[RefreshCookie.Name];
        return !string.IsNullOrWhiteSpace(token);
    }

    private static IResult Reject(HttpContext http)
    {
        if (!IsNativeClient(http.Request)) ClearCookie(http);
        return Results.Unauthorized();
    }

    private static IResult Session(HttpContext http, AppSettings settings, User user, JwtTokenService tokens, string refreshToken)
    {
        var native = IsNativeClient(http.Request);
        if (!native) http.Response.Cookies.Append(RefreshCookie.Name, refreshToken, RefreshCookie.Options(http.Request, settings.RefreshTokenLifetime));
        return Results.Ok(new
        {
            accessToken = tokens.Create(user),
            refreshToken = native ? refreshToken : null,
            expiresIn = tokens.ExpiresInSeconds,
            role = user.Role.ToString()
        });
    }

    private static void ClearCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookie.Name, RefreshCookie.Options(http.Request, TimeSpan.Zero));
}

internal static class RefreshCookie
{
    public const string Name = "mieu_rt";
    public const string CsrfHeader = "X-Requested-With";

    public static CookieOptions Options(HttpRequest request, TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,          // plain http only for local development
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",             // sent only to the auth endpoints, never with ordinary API calls
        IsEssential = true,
        Expires = lifetime > TimeSpan.Zero ? DateTimeOffset.UtcNow.Add(lifetime) : null
    };
}
