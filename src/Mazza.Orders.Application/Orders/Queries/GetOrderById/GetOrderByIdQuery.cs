using MediatR;
using Mazza.Orders.Application.Orders.Dtos;

namespace Mazza.Orders.Application.Orders.Queries.GetOrderById;

/// <summary>
/// Retrieves a single order, including its lines.
/// </summary>
/// <param name="OrderId">Order to retrieve.</param>
public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDto>;
