using HongBeautyPOS.Api.Contracts;
using HongBeautyPOS.Api.Security;
using HongBeautyPOS.Api.Startup;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Endpoints;

internal static class AuthEndpoints
{
    // Verified when the email is unknown so response time does not reveal which accounts exist.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-equalizer-" + Guid.NewGuid());

    internal static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", async (LoginRequest request, PosDbContext db, JwtTokenService tokens) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email && u.Active);
            var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
            if (user is null || !passwordMatches) return Results.Unauthorized();
            return Results.Ok(new { accessToken = tokens.Create(user), role = user.Role.ToString() });
        }).RequireRateLimiting(RateLimitPolicies.Login);
        return api;
    }
}
