using Mazza.Orders.Domain.Common;

namespace Mazza.Orders.Domain.Orders;

/// <summary>
/// A line inside an <see cref="Order"/>. Part of the Order aggregate: it is never
/// loaded, created or saved on its own, which is why the constructor is internal
/// and the only way to get one is through <see cref="Order.Create"/>.
/// </summary>
public sealed class OrderItem : Entity
{
    public const int MaxProductNameLength = 200;

    internal OrderItem(Guid id, Guid orderId, string productName, int quantity, decimal unitPrice)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("Order item product name is required.");
        }

        if (productName.Length > MaxProductNameLength)
        {
            throw new DomainException(
                $"Order item product name cannot exceed {MaxProductNameLength} characters.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Order item quantity must be greater than zero.");
        }

        if (unitPrice <= 0m)
        {
            throw new DomainException("Order item unit price must be greater than zero.");
        }

        OrderId = orderId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>Parameterless ctor reserved for the EF Core materializer.</summary>
    private OrderItem()
    {
        ProductName = string.Empty;
    }

    public Guid OrderId { get; private set; }

    public string ProductName { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// Value of this line. Lives here rather than in the handler so there is a single
    /// definition of "what a line costs" for the aggregate to sum over.
    /// </summary>
    public decimal LineTotal => UnitPrice * Quantity;
}
