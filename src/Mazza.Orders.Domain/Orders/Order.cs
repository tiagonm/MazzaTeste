using Mazza.Orders.Domain.Common;

namespace Mazza.Orders.Domain.Orders;

/// <summary>
/// Aggregate root for an order.
///
/// Every rule in the test statement is enforced here rather than in a handler:
/// an order needs at least one item, quantities and prices must be positive,
/// only a <see cref="OrderStatus.Pending"/> order can be cancelled, and
/// <see cref="TotalAmount"/> is computed from the lines. The Application layer
/// only orchestrates - it never decides.
/// </summary>
public sealed class Order : Entity
{
    private readonly List<OrderItem> _items = [];

    private Order(Guid id, Guid customerId, DateTime createdAtUtc)
        : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Order customer id is required.");
        }

        CustomerId = customerId;
        CreatedAt = createdAtUtc;
        Status = OrderStatus.Pending;
    }

    /// <summary>Parameterless ctor reserved for the EF Core materializer.</summary>
    private Order()
    {
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Exposed read-only: callers cannot bypass the aggregate to bolt an item on.
    /// EF Core writes through the backing field, configured in <c>OrderConfiguration</c>.
    /// </summary>
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>
    /// Sum of <c>UnitPrice * Quantity</c> across all lines.
    ///
    /// Computed rather than stored, so it cannot drift away from the items it
    /// summarises. Read paths therefore have to load the lines; see the README for
    /// why that trade-off is the right one at this scale.
    /// </summary>
    public decimal TotalAmount => _items.Sum(item => item.LineTotal);

    /// <summary>
    /// The only way to bring an order into existence.
    /// </summary>
    /// <param name="customerId">Customer placing the order.</param>
    /// <param name="items">At least one line; each one is validated by <see cref="OrderItem"/>.</param>
    /// <param name="createdAtUtc">
    /// Supplied by the caller (from <see cref="TimeProvider"/>) instead of read from
    /// <c>DateTime.UtcNow</c>, which keeps the aggregate deterministic under test.
    /// </param>
    /// <exception cref="DomainException">If the order would violate an invariant.</exception>
    public static Order Create(Guid customerId, IEnumerable<NewOrderItem> items, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(items);

        var order = new Order(Guid.CreateVersion7(), customerId, createdAtUtc);

        foreach (var item in items)
        {
            order._items.Add(new OrderItem(
                Guid.CreateVersion7(),
                order.Id,
                item.ProductName,
                item.Quantity,
                item.UnitPrice));
        }

        if (order._items.Count == 0)
        {
            throw new DomainException("An order must have at least one item.");
        }

        return order;
    }

    /// <summary>
    /// Cancels the order. Only valid while the order is still
    /// <see cref="OrderStatus.Pending"/>.
    /// </summary>
    /// <exception cref="InvalidOrderStateException">If the order is not pending.</exception>
    public void Cancel()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOrderStateException(
                $"Only pending orders can be cancelled. Order {Id} is {Status}.");
        }

        Status = OrderStatus.Cancelled;
    }

    /// <summary>
    /// Confirms the order. Only valid while the order is still
    /// <see cref="OrderStatus.Pending"/>.
    /// </summary>
    /// <exception cref="InvalidOrderStateException">If the order is not pending.</exception>
    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOrderStateException(
                $"Only pending orders can be confirmed. Order {Id} is {Status}.");
        }

        Status = OrderStatus.Confirmed;
    }
}
