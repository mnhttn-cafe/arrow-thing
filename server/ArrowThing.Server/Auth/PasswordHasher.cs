using System.Security.Cryptography;

namespace ArrowThing.Server.Auth;

public static class PasswordHasher
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);

    /// <summary>
    /// Hashes a short-lived OTP code using bcrypt. With ~900k possible 6-digit codes
    /// and bcrypt's ~100ms per attempt, offline brute-force takes ~25 hours — well
    /// beyond the 10-minute code lifetime.
    /// Uses a lower work factor (8) than passwords since the codes are ephemeral.
    /// </summary>
    public static string HashOtp(string code) =>
        BCrypt.Net.BCrypt.HashPassword(code, workFactor: 8);

    public static bool VerifyOtp(string code, string hash) => BCrypt.Net.BCrypt.Verify(code, hash);

    /// <summary>
    /// Generates a cryptographically secure 6-digit verification code.
    /// </summary>
    public static string GenerateSecureCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks.
    /// </summary>
    public static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null)
            return a == b;

        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
