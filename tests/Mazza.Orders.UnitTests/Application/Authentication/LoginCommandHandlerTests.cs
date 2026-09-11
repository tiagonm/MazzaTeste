using Mazza.Orders.Application.Authentication.Commands.Login;
using Mazza.Orders.Application.Common.Abstractions.Identity;
using Mazza.Orders.Application.Common.Exceptions;
using Mazza.Orders.UnitTests.TestSupport;
using NSubstitute;

namespace Mazza.Orders.UnitTests.Application.Authentication;

/// <summary>
/// Tests for <see cref="LoginCommandHandler"/>.
/// </summary>
public sealed class LoginCommandHandlerTests
{
    private static readonly UserAccount KnownUser =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "dev@martech.com");

    private readonly IUserAuthenticator _authenticator = Substitute.For<IUserAuthenticator>();
    private readonly IAccessTokenFactory _tokenFactory = Substitute.For<IAccessTokenFactory>();
    private readonly FixedTimeProvider _timeProvider = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_authenticator, _tokenFactory, _timeProvider);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsABearerToken()
    {
        var expiresAt = FixedTimeProvider.DefaultNow.AddMinutes(60);
        GivenValidCredentials();
        _tokenFactory.Create(KnownUser).Returns(new AccessToken("signed.jwt.value", expiresAt));

        var result = await _handler.Handle(
            new LoginCommand("dev@martech.com", "Senha@123"),
            CancellationToken.None);

        Assert.Equal("signed.jwt.value", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(expiresAt, result.ExpiresAtUtc);
    }

    [Fact]
    public async Task Handle_DerivesExpiresInFromTheClockRatherThanAssumingIt()
    {
        GivenValidCredentials();
        _tokenFactory.Create(KnownUser)
            .Returns(new AccessToken("token", FixedTimeProvider.DefaultNow.AddMinutes(15)));

        var result = await _handler.Handle(
            new LoginCommand("dev@martech.com", "Senha@123"),
            CancellationToken.None);

        Assert.Equal(900, result.ExpiresInSeconds);
    }

    [Fact]
    public async Task Handle_WithAnAlreadyExpiredToken_ReportsZeroRatherThanANegativeLifetime()
    {
        GivenValidCredentials();
        _tokenFactory.Create(KnownUser)
            .Returns(new AccessToken("token", FixedTimeProvider.DefaultNow.AddMinutes(-5)));

        var result = await _handler.Handle(
            new LoginCommand("dev@martech.com", "Senha@123"),
            CancellationToken.None);

        // A negative expires_in would be nonsense to any client reading it.
        Assert.Equal(0, result.ExpiresInSeconds);
    }

    [Fact]
    public async Task Handle_WithInvalidCredentials_ThrowsAndMintsNoToken()
    {
        _authenticator
            .AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserAccount?)null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.Handle(new LoginCommand("dev@martech.com", "wrong"), CancellationToken.None));

        // No token is created for a failed login - not even one that is thrown away.
        _tokenFactory.DidNotReceive().Create(Arg.Any<UserAccount>());
    }

    [Fact]
    public async Task Handle_PassesTheSubmittedCredentialsThroughUnchanged()
    {
        GivenValidCredentials();
        _tokenFactory.Create(KnownUser)
            .Returns(new AccessToken("token", FixedTimeProvider.DefaultNow.AddMinutes(60)));

        await _handler.Handle(new LoginCommand("dev@martech.com", "Senha@123"), CancellationToken.None);

        await _authenticator.Received(1)
            .AuthenticateAsync("dev@martech.com", "Senha@123", Arg.Any<CancellationToken>());
    }

    private void GivenValidCredentials() =>
        _authenticator
            .AuthenticateAsync("dev@martech.com", "Senha@123", Arg.Any<CancellationToken>())
            .Returns(KnownUser);
}
