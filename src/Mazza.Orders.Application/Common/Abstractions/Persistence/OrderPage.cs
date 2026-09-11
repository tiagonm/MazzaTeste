using Mazza.Orders.Domain.Orders;

namespace Mazza.Orders.Application.Common.Abstractions.Persistence;

/// <summary>
/// One page of orders plus the total number of rows that matched.
/// Returned as a single object so a page read costs one round trip to the
/// repository instead of a separate "get items" and "count" call.
/// </summary>
/// <param name="Orders">The orders on the requested page.</param>
/// <param name="TotalCount">Total orders available, ignoring pagination.</param>
public sealed record OrderPage(IReadOnlyList<Order> Orders, int TotalCount);
