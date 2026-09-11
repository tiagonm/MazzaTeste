using MediatR;
using Mazza.Orders.Application.Common.Models;
using Mazza.Orders.Application.Orders.Dtos;

namespace Mazza.Orders.Application.Orders.Queries.GetOrders;

/// <summary>
/// Lists orders, newest first, one page at a time.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record GetOrdersQuery(int Page = GetOrdersQuery.DefaultPage, int PageSize = GetOrdersQuery.DefaultPageSize)
    : IRequest<PagedResult<OrderSummaryDto>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Upper bound on <c>pageSize</c>. Without a ceiling, <c>?pageSize=1000000</c> is
    /// a denial-of-service vector against both the database and the serialiser.
    /// </summary>
    public const int MaxPageSize = 100;
}
