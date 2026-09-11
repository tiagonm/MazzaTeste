namespace Mazza.Orders.Application.Orders.Dtos;

/// <summary>
/// Full representation of an order, used by the create, get-by-id and cancel responses.
/// </summary>
/// <param name="Id">Order identifier.</param>
/// <param name="CustomerId">Customer who placed the order.</param>
/// <param name="Status">
/// The status <em>name</em> ("Pending", "Confirmed", "Cancelled") rather than the
/// underlying number. Clients get a self-describing contract that does not break if
/// the enum's numeric values are ever reordered.
/// </param>
/// <param name="CreatedAt">Creation timestamp, UTC.</param>
/// <param name="TotalAmount">Order total, computed by the domain.</param>
/// <param name="Items">The order lines.</param>
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    DateTime CreatedAt,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);
