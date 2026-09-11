using Mazza.Orders.Application.Common.Abstractions.Identity;
using Microsoft.Extensions.Options;

namespace Mazza.Orders.Infrastructure.Identity;

/// <summary>
/// Authenticates against the single configured development account.
///
/// Registered as a singleton so the (deliberately expensive) PBKDF2 hash of the
/// configured password is computed once at startup rather than on every login.
/// </summary>
internal sealed class InMemoryUserAuthenticator : IUserAuthenticator
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly UserAccount _account;
    private readonly string _passwordHash;

    public InMemoryUserAuthenticator(IOptions<DevUserOptions> options, IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;

        var devUser = options.Value;

        _account = new UserAccount(devUser.Id, devUser.Email);
        _passwordHash = passwordHasher.Hash(devUser.Password);
    }

    public Task<UserAccount?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var emailMatches = string.Equals(email, _account.Email, StringComparison.OrdinalIgnoreCase);

        // The hash is verified even when the email does not match, so a request for an
        // unknown account takes the same time as one for a known account with the wrong
        // password. Otherwise the response time alone reveals which accounts exist.
        var passwordMatches = _passwordHasher.Verify(password, _passwordHash);

        var authenticated = emailMatches && passwordMatches;

        return Task.FromResult(authenticated ? _account : null);
    }
}
