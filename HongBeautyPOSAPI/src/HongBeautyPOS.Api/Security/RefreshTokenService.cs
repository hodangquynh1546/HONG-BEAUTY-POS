using System.Security.Cryptography;
using System.Text;
using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Domain;
using HongBeautyPOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Api.Security;

/// <summary>
/// Opaque, rotating refresh tokens. Each use returns a new token and revokes the old one; replaying a used
/// token revokes the whole login (family), which limits the damage of a leaked token.
/// </summary>
internal sealed class RefreshTokenService(PosDbContext db, AppSettings settings)
{
    /// <summary>A just-rotated token may be presented again briefly (two tabs, retry after a dropped response).</summary>
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(15);

    public sealed record Result(User User, string RefreshToken);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public async Task<string> IssueAsync(User user, CancellationToken ct, Guid? familyId = null, RefreshToken? replacing = null)
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        var entry = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId ?? Guid.NewGuid(),
            TokenHash = Hash(raw),
            PasswordVersion = JwtTokenService.PasswordVersion(user.PasswordHash),
            ExpiresAt = DateTimeOffset.UtcNow.Add(settings.RefreshTokenLifetime)
        };
        db.RefreshTokens.Add(entry);
        if (replacing is not null)
        {
            replacing.RevokedAt = DateTimeOffset.UtcNow;
            replacing.ReplacedById = entry.Id;
        }
        await db.SaveChangesAsync(ct);
        // Housekeeping: drop this user's tokens that expired more than a week ago.
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
        return raw;
    }

    /// <summary>Validates and rotates a refresh token. Returns null for any invalid, expired, revoked or replayed token.</summary>
    public async Task<Result?> RotateAsync(string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 128) return null;
        var hash = Hash(raw);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return null;

        if (token.RevokedAt is { } revokedAt)
        {
            if (DateTimeOffset.UtcNow - revokedAt > ReuseGrace) await RevokeFamilyAsync(token.FamilyId, ct);
            return null;
        }
        if (token.ExpiresAt <= DateTimeOffset.UtcNow) return null;

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId && u.Active, ct);
        if (user is null || !string.Equals(token.PasswordVersion, JwtTokenService.PasswordVersion(user.PasswordHash), StringComparison.Ordinal))
        {
            await RevokeFamilyAsync(token.FamilyId, ct);
            return null;
        }
        return new Result(user, await IssueAsync(user, ct, token.FamilyId, token));
    }

    /// <summary>Ends the login that owns this token (logout). Unknown tokens are ignored.</summary>
    public async Task RevokeAsync(string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 128) return;
        var hash = Hash(raw);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(ct);
        if (familyId is { } id) await RevokeFamilyAsync(id, ct);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
