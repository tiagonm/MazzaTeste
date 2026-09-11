using Mazza.Orders.Application.Common.Models;
using Mazza.Orders.Application.Orders.Commands.CancelOrder;
using Mazza.Orders.Application.Orders.Commands.CreateOrder;
using Mazza.Orders.Application.Orders.Dtos;
using Mazza.Orders.Application.Orders.Queries.GetOrderById;
using Mazza.Orders.Application.Orders.Queries.GetOrders;
using MediatR;

namespace Mazza.Orders.Api.Endpoints;

/// <summary>
/// Order routes. Every one of them is a two-line adapter: build the request, send it,
/// pick the status code. That is the entire responsibility of this layer.
/// </summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization on the group rather than on each route: a new order
        // endpoint is protected the moment it is added, instead of relying on whoever
        // adds it to remember the attribute.
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapPost("", CreateOrderAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates a new order.")
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("", GetOrdersAsync)
            .WithName("GetOrders")
            .WithSummary("Lists orders, newest first, with pagination.")
            .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetOrderByIdAsync)
            .WithName("GetOrderById")
            .WithSummary("Returns a single order with its items.")
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/cancel", CancelOrderAsync)
            .WithName("CancelOrder")
            .WithSummary("Cancels a pending order.")
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> CreateOrderAsync(
        CreateOrderCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var order = await sender.Send(command, cancellationToken);

        // 201 with a Location header pointing at the new resource, which is what a
        // client needs to follow up without guessing the URL.
        return TypedResults.Created($"/api/orders/{order.Id}", order);
    }

    private static async Task<IResult> GetOrdersAsync(
        ISender sender,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null)
    {
        // Absent query parameters fall back to the query's own defaults; a parameter
        // that is present but nonsense (page=0) is rejected by the validator rather
        // than silently corrected.
        var query = new GetOrdersQuery(
            page ?? GetOrdersQuery.DefaultPage,
            pageSize ?? GetOrdersQuery.DefaultPageSize);

        var orders = await sender.Send(query, cancellationToken);

        return TypedResults.Ok(orders);
    }

    private static async Task<IResult> GetOrderByIdAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var order = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);

        return TypedResults.Ok(order);
    }

    private static async Task<IResult> CancelOrderAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var order = await sender.Send(new CancelOrderCommand(id), cancellationToken);

        return TypedResults.Ok(order);
    }
}
