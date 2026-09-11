namespace Mazza.Orders.Application.Orders.Dtos;

/// <summary>
/// Lighter projection used by the paginated list, which does not need every line of
/// every order - just enough to render a row and drill in.
/// </summary>
/// <param name="Id">Order identifier.</param>
/// <param name="CustomerId">Customer who placed the order.</param>
/// <param name="Status">Status name.</param>
/// <param name="CreatedAt">Creation timestamp, UTC.</param>
/// <param name="TotalAmount">Order total, computed by the domain.</param>
/// <param name="ItemCount">How many lines the order has.</param>
public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    DateTime CreatedAt,
    decimal TotalAmount,
    int ItemCount);
