using MediatR;
using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Orders.Dtos;
using Mazza.Orders.Application.Orders.Mapping;
using Mazza.Orders.Domain.Orders;

namespace Mazza.Orders.Application.Orders.Commands.CreateOrder;

/// <summary>
/// Orchestrates order creation. Note how little it does: translate the command into
/// the domain's input shape, let the aggregate build (and validate) itself, persist,
/// commit, project. Every business decision - including the total - is made inside
/// <see cref="Order"/>.
/// </summary>
public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateOrderCommandHandler(
        IOrderRepository orders,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // TimeProvider (BCL, .NET 8+) instead of DateTime.UtcNow: the clock is a
        // dependency like any other, so tests can pin it rather than tolerate drift.
        var createdAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var order = Order.Create(
            request.CustomerId,
            request.Items.Select(item => new NewOrderItem(item.ProductName, item.Quantity, item.UnitPrice)),
            createdAtUtc);

        _orders.Add(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }
}
