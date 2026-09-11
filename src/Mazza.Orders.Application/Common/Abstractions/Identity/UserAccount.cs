namespace Mazza.Orders.Application.Common.Abstractions.Identity;

/// <summary>
/// A successfully authenticated user, as far as this application cares.
/// Note what is absent: no password, no hash, no claims plumbing. Credentials never
/// leave the Infrastructure component that verifies them.
/// </summary>
/// <param name="Id">Stable identifier, used as the token subject.</param>
/// <param name="Email">Login of the user.</param>
public sealed record UserAccount(Guid Id, string Email);
