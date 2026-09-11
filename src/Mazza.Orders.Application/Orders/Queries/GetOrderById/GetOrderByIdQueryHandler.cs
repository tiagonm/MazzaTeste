using MediatR;
using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Common.Exceptions;
using Mazza.Orders.Application.Orders.Dtos;
using Mazza.Orders.Application.Orders.Mapping;

namespace Mazza.Orders.Application.Orders.Queries.GetOrderById;

/// <summary>
/// Reads one order and projects it. Throws <see cref="NotFoundException"/> rather than
/// returning null, so the "missing" case is handled in exactly one place (the API's
/// exception handler) instead of at every call site.
/// </summary>
public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IOrderRepository _orders;

    public GetOrderByIdQueryHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.FindByIdAsync(request.OrderId, cancellationToken)
            ?? throw NotFoundException.ForOrder(request.OrderId);

        return order.ToDto();
    }
}
