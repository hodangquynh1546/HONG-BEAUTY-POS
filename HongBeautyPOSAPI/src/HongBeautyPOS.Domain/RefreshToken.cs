namespace HongBeautyPOS.Domain;

/// <summary>
/// One rotating refresh credential. Only the SHA-256 hash of the secret is stored.
/// Tokens of the same login share a <see cref="FamilyId"/> so a replayed (already used) token can revoke the whole login.
/// </summary>
public sealed class RefreshToken
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid UserId { get; set; }

	public Guid FamilyId { get; set; }

	public required string TokenHash { get; set; }

	/// <summary>Password version at issue time; a password change invalidates every refresh token.</summary>
	public required string PasswordVersion { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset ExpiresAt { get; set; }

	public DateTimeOffset? RevokedAt { get; set; }

	public Guid? ReplacedById { get; set; }
}
