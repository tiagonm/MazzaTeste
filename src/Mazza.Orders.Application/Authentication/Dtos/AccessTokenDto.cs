namespace Mazza.Orders.Application.Authentication.Dtos;

/// <summary>
/// Login response. Shaped like the OAuth 2.0 bearer token response so clients can use
/// it with an off-the-shelf HTTP layer instead of a bespoke one.
/// </summary>
/// <param name="AccessToken">The encoded JWT.</param>
/// <param name="TokenType">Always "Bearer" - the scheme to put in the Authorization header.</param>
/// <param name="ExpiresAtUtc">Absolute expiry instant.</param>
/// <param name="ExpiresInSeconds">Relative lifetime, for clients that prefer a countdown.</param>
public sealed record AccessTokenDto(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    int ExpiresInSeconds);
