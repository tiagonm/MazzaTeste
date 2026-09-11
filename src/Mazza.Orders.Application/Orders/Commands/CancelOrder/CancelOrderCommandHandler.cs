using MediatR;
using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Common.Exceptions;
using Mazza.Orders.Application.Orders.Dtos;
using Mazza.Orders.Application.Orders.Mapping;

namespace Mazza.Orders.Application.Orders.Commands.CancelOrder;

/// <summary>
/// Loads the order, asks it to cancel itself, commits.
///
/// The "only pending orders can be cancelled" rule is nowhere in this file - it lives
/// in <c>Order.Cancel()</c>. If it were checked here, a second call site could cancel
/// a shipped order simply by forgetting the same <c>if</c>.
/// </summary>
public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, OrderDto>
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderCommandHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDto> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.FindByIdAsync(request.OrderId, cancellationToken)
            ?? throw NotFoundException.ForOrder(request.OrderId);

        order.Cancel();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }
}
