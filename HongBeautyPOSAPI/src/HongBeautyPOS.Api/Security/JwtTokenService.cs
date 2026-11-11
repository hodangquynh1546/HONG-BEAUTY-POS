using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Domain;
using Microsoft.IdentityModel.Tokens;

namespace HongBeautyPOS.Api.Security;

internal sealed class JwtTokenService(AppSettings settings)
{
    private readonly SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(settings.JwtSecret));

    public int ExpiresInSeconds => (int)settings.AccessTokenLifetime.TotalSeconds;

    /// <summary>Changes whenever the password changes, which invalidates every previously issued token.</summary>
    public static string PasswordVersion(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));

    public string Create(User user)
    {
        var token = new JwtSecurityToken(
            issuer: AppSettings.JwtIssuer, audience: AppSettings.JwtAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("pwdv", PasswordVersion(user.PasswordHash))
            ],
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.Add(settings.AccessTokenLifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
