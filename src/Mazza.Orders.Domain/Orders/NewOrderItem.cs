namespace Mazza.Orders.Domain.Orders;

/// <summary>
/// Input shape for <see cref="Order.Create"/>.
///
/// The Domain declares what it needs to build an order instead of accepting an
/// Application DTO, which keeps the dependency arrow pointing inwards: Application
/// maps its command into this, never the other way round.
/// </summary>
/// <param name="ProductName">Free-text description of the product being ordered.</param>
/// <param name="Quantity">How many units - must be greater than zero.</param>
/// <param name="UnitPrice">Price per unit - must be greater than zero.</param>
public readonly record struct NewOrderItem(string ProductName, int Quantity, decimal UnitPrice);
