using Mazza.Orders.Application.Authentication.Commands.Login;
using Mazza.Orders.Application.Authentication.Dtos;
using MediatR;

namespace Mazza.Orders.Api.Endpoints;

/// <summary>
/// Authentication routes.
/// </summary>
public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Exchanges credentials for a JWT bearer token.")
            .Produces<AccessTokenDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>
    /// The endpoint's whole job: hand the request to MediatR and turn the result into
    /// an HTTP response. No branching, no rules - if a rule ever needs to be added,
    /// it belongs in the handler or the domain, not here.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var token = await sender.Send(command, cancellationToken);

        return TypedResults.Ok(token);
    }
}
