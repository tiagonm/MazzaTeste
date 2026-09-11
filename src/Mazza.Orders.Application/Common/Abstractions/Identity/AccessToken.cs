namespace Mazza.Orders.Application.Common.Abstractions.Identity;

/// <summary>
/// A minted bearer token and the moment it stops being valid.
/// </summary>
/// <param name="Value">The encoded JWT.</param>
/// <param name="ExpiresAtUtc">Absolute expiry, so clients can refresh before it lapses.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);
