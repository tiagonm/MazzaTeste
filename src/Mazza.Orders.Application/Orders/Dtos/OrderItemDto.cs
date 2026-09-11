namespace Mazza.Orders.Application.Orders.Dtos;

/// <summary>
/// A single order line as returned by the API.
/// </summary>
/// <param name="Id">Line identifier.</param>
/// <param name="ProductName">Product description.</param>
/// <param name="Quantity">Units ordered.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="LineTotal">
/// <c>UnitPrice * Quantity</c>, taken from the domain so the client never has to
/// recompute it (and cannot disagree with the server about it).
/// </param>
public sealed record OrderItemDto(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
