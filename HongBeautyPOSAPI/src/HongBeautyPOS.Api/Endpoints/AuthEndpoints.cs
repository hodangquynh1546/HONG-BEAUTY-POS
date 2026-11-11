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
        api.MapPost("/auth/login", async (LoginRequest request, PosDbContext db, JwtTokenService tokens, RefreshTokenService refresh, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email && u.Active, ct);
            var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
            if (user is null || !passwordMatches) return Results.Unauthorized();
            return Results.Ok(Session(user, tokens, await refresh.IssueAsync(user, ct)));
        }).RequireRateLimiting(RateLimitPolicies.Login);

        api.MapPost("/auth/refresh", async (RefreshRequest request, JwtTokenService tokens, RefreshTokenService refresh, CancellationToken ct) =>
        {
            var result = await refresh.RotateAsync(request.RefreshToken, ct);
            return result is null ? Results.Unauthorized() : Results.Ok(Session(result.User, tokens, result.RefreshToken));
        }).RequireRateLimiting(RateLimitPolicies.Session);

        api.MapPost("/auth/logout", async (RefreshRequest request, RefreshTokenService refresh, CancellationToken ct) =>
        {
            await refresh.RevokeAsync(request.RefreshToken, ct);
            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.Session);
        return api;
    }

    private static object Session(User user, JwtTokenService tokens, string refreshToken) => new
    {
        accessToken = tokens.Create(user),
        refreshToken,
        expiresIn = tokens.ExpiresInSeconds,
        role = user.Role.ToString()
    };
}
