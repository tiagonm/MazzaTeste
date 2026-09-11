namespace Mazza.Orders.Domain.Orders;

/// <summary>
/// Lifecycle of an <see cref="Order"/>.
///
/// Persisted as text ("Pending") rather than as a number - see
/// <c>OrderConfiguration</c> - so reordering these members cannot silently
/// reinterpret existing rows. The explicit values are there for readability.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
}
