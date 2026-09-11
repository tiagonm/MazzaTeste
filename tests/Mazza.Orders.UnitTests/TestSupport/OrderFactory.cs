using Mazza.Orders.Domain.Orders;

namespace Mazza.Orders.UnitTests.TestSupport;

/// <summary>
/// Builds valid orders so each test only has to state the part it actually cares
/// about. Keeps the arrange step of a test about the scenario rather than about
/// filling in required fields.
/// </summary>
internal static class OrderFactory
{
    public static readonly Guid DefaultCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A one-line, one-item order worth 100.00.</summary>
    public static Order Pending(
        Guid? customerId = null,
        DateTime? createdAtUtc = null,
        params NewOrderItem[] items) =>
        Order.Create(
            customerId ?? DefaultCustomerId,
            items.Length > 0 ? items : [new NewOrderItem("Keyboard", 1, 100.00m)],
            createdAtUtc ?? FixedTimeProvider.DefaultNow.UtcDateTime);

    /// <summary>An order that has already been cancelled.</summary>
    public static Order Cancelled()
    {
        var order = Pending();
        order.Cancel();
        return order;
    }

    /// <summary>An order that has already been confirmed.</summary>
    public static Order Confirmed()
    {
        var order = Pending();
        order.Confirm();
        return order;
    }
}
