using MediatR;
using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Common.Models;
using Mazza.Orders.Application.Orders.Dtos;
using Mazza.Orders.Application.Orders.Mapping;

namespace Mazza.Orders.Application.Orders.Queries.GetOrders;

/// <summary>
/// Reads one page of orders and wraps it in a <see cref="PagedResult{T}"/>.
/// </summary>
public sealed class GetOrdersQueryHandler
    : IRequestHandler<GetOrdersQuery, PagedResult<OrderSummaryDto>>
{
    private readonly IOrderRepository _orders;

    public GetOrdersQueryHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<PagedResult<OrderSummaryDto>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var page = await _orders.GetPageAsync(request.Page, request.PageSize, cancellationToken);

        return new PagedResult<OrderSummaryDto>(
            [.. page.Orders.Select(order => order.ToSummaryDto())],
            request.Page,
            request.PageSize,
            page.TotalCount);
    }
}
