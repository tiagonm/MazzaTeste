namespace Mazza.Orders.Application.Common.Abstractions.Identity;

/// <summary>
/// Verifies a set of credentials. The Application layer only needs the answer
/// ("who is this, if anyone?"), so how users are stored - in-memory for this test,
/// a database or an identity provider later - stays entirely in Infrastructure.
/// </summary>
public interface IUserAuthenticator
{
    /// <summary>
    /// Returns the matching account, or <c>null</c> when the credentials are invalid.
    /// A single null result for both "unknown user" and "wrong password" keeps the
    /// endpoint from leaking which accounts exist.
    /// </summary>
    Task<UserAccount?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}
