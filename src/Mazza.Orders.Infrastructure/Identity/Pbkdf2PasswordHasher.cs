using System.Globalization;
using System.Security.Cryptography;

namespace Mazza.Orders.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing.
///
/// The test says an in-memory user is enough, and it is - but comparing the submitted
/// password to a stored string with <c>==</c> would bake two bad habits into the code:
/// a plaintext credential at rest and a comparison that leaks its answer through
/// timing. Doing it properly here costs one small class and means swapping the
/// in-memory store for a real one changes nothing about how credentials are checked.
///
/// Format: <c>{iterations}.{base64 salt}.{base64 hash}</c> - self-describing, so the
/// iteration count can be raised later without invalidating existing hashes.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000; // OWASP guidance for PBKDF2-HMAC-SHA256.
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    private const char Separator = '.';

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join(
            Separator,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split(Separator);

        if (parts.Length != 3
            || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        // Fixed-time comparison: a plain SequenceEqual returns as soon as two bytes
        // differ, which tells an attacker how much of a guess was correct.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
