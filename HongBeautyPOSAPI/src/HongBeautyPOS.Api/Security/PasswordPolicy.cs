namespace HongBeautyPOS.Api.Security;

/// <summary>Single source of truth for password rules (owner, employee, self-service).</summary>
internal static class PasswordPolicy
{
    public const int MinLength = 14;
    public const int MaxLength = 128;
    public const string Requirement = "Mật khẩu mới phải dài 14–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt";

    public static bool IsStrong(string? password) =>
        !string.IsNullOrEmpty(password) &&
        !password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) &&
        password.Length is >= MinLength and <= MaxLength &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) &&
        !password.All(char.IsLetterOrDigit);
}
