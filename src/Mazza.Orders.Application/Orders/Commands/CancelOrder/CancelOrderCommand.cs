using MediatR;
using Mazza.Orders.Application.Orders.Dtos;

namespace Mazza.Orders.Application.Orders.Commands.CancelOrder;

/// <summary>
/// Cancels a pending order.
///
/// Returns the updated <see cref="OrderDto"/> rather than nothing: the caller just
/// changed the resource's state and the new state is the one thing it certainly wants
/// to see, so handing it back saves an immediate follow-up GET.
/// </summary>
/// <param name="OrderId">Order to cancel.</param>
public sealed record CancelOrderCommand(Guid OrderId) : IRequest<OrderDto>;
