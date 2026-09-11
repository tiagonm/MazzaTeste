using System.ComponentModel.DataAnnotations;

namespace Mazza.Orders.Infrastructure.Identity;

/// <summary>
/// JWT signing and validation settings, bound from the <c>Jwt</c> configuration
/// section and validated at startup.
///
/// The data annotations are not decoration: they are wired to
/// <c>ValidateDataAnnotations().ValidateOnStart()</c>, so a deployment with a missing
/// or too-short signing key fails immediately and loudly instead of starting up and
/// issuing tokens that cannot be trusted.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signing key.
    ///
    /// Never has a default and is never committed: it is supplied by the environment
    /// (<c>Jwt__SigningKey</c>), user-secrets or a secret store. The composition root
    /// generates a random one per process in Development so the application still
    /// runs with no setup; anywhere else, a missing key stops startup.
    ///
    /// The 32-character floor is the algorithm's own requirement: HS256 uses a
    /// 256-bit key, and a shorter secret makes the signature weaker than the
    /// algorithm advertises.
    /// </summary>
    [Required(
        ErrorMessage = "Jwt:SigningKey is not configured. Set the Jwt__SigningKey "
            + "environment variable (or use dotnet user-secrets / a secret store). "
            + "It must be at least 32 characters.")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters for HS256.")]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 60;

    /// <summary>
    /// Tolerance for clock drift between this service and whoever validates the token.
    /// The default in the JWT middleware is five minutes, which means an expired token
    /// keeps working for five more minutes; 30 seconds is enough for real drift.
    /// </summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; init; } = 30;
}
