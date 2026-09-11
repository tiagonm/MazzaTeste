using Mazza.Orders.Application.Orders.Dtos;
using Mazza.Orders.Domain.Orders;

namespace Mazza.Orders.Application.Orders.Mapping;

/// <summary>
/// Hand-written projections from the Order aggregate to its DTOs.
///
/// No AutoMapper on purpose: for a handful of types, explicit mapping is shorter to
/// read than the configuration it would replace, breaks at compile time instead of at
/// runtime when a property is renamed, and leaves nothing for a reviewer to guess at.
/// </summary>
internal static class OrderMappings
{
    public static OrderDto ToDto(this Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.CreatedAt,
            order.TotalAmount,
            [.. order.Items.Select(ToDto)]);

    public static OrderSummaryDto ToSummaryDto(this Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.CreatedAt,
            order.TotalAmount,
            order.Items.Count);

    private static OrderItemDto ToDto(OrderItem item) =>
        new(item.Id, item.ProductName, item.Quantity, item.UnitPrice, item.LineTotal);
}
