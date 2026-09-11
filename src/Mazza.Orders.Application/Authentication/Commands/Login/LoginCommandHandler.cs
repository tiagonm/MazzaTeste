using MediatR;
using Mazza.Orders.Application.Authentication.Dtos;
using Mazza.Orders.Application.Common.Abstractions.Identity;
using Mazza.Orders.Application.Common.Exceptions;

namespace Mazza.Orders.Application.Authentication.Commands.Login;

/// <summary>
/// Verifies credentials and mints a token. Both steps are delegated to Infrastructure
/// ports, so this handler holds no knowledge of password hashing or JWT signing.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AccessTokenDto>
{
    private readonly IUserAuthenticator _authenticator;
    private readonly IAccessTokenFactory _tokenFactory;
    private readonly TimeProvider _timeProvider;

    public LoginCommandHandler(
        IUserAuthenticator authenticator,
        IAccessTokenFactory tokenFactory,
        TimeProvider timeProvider)
    {
        _authenticator = authenticator;
        _tokenFactory = tokenFactory;
        _timeProvider = timeProvider;
    }

    public async Task<AccessTokenDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _authenticator.AuthenticateAsync(request.Email, request.Password, cancellationToken)
            ?? throw new AuthenticationFailedException();

        var token = _tokenFactory.Create(user);

        var expiresIn = (int)Math.Max(0, (token.ExpiresAtUtc - _timeProvider.GetUtcNow()).TotalSeconds);

        return new AccessTokenDto(token.Value, "Bearer", token.ExpiresAtUtc, expiresIn);
    }
}
