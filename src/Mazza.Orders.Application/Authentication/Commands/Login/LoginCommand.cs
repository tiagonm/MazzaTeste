using MediatR;
using Mazza.Orders.Application.Authentication.Dtos;

namespace Mazza.Orders.Application.Authentication.Commands.Login;

/// <summary>
/// Exchanges credentials for a bearer token.
/// </summary>
/// <param name="Email">User login.</param>
/// <param name="Password">User password.</param>
public sealed record LoginCommand(string Email, string Password) : IRequest<AccessTokenDto>;
