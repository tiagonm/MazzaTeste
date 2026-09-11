using System.Globalization;
using Mazza.Orders.Domain.Common;
using Mazza.Orders.Domain.Orders;
using Mazza.Orders.UnitTests.TestSupport;

namespace Mazza.Orders.UnitTests.Domain;

/// <summary>
/// Tests for the Order aggregate. These are the most valuable tests in the suite:
/// they need no mocks and no database, because every rule they exercise lives in a
/// class with no dependencies.
/// </summary>
public sealed class OrderTests
{
    private static readonly DateTime CreatedAt = FixedTimeProvider.DefaultNow.UtcDateTime;

    [Fact]
    public void Create_WithValidItems_StartsPendingAndKeepsTheSuppliedTimestamp()
    {
        var order = Order.Create(
            OrderFactory.DefaultCustomerId,
            [new NewOrderItem("Mouse", 2, 50.00m)],
            CreatedAt);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(CreatedAt, order.CreatedAt);
        Assert.Equal(OrderFactory.DefaultCustomerId, order.CustomerId);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Fact]
    public void Create_LinksEveryItemToTheOrderAndGivesEachOneAnIdentity()
    {
        var order = Order.Create(
            OrderFactory.DefaultCustomerId,
            [new NewOrderItem("Mouse", 1, 50.00m), new NewOrderItem("Pad", 1, 15.00m)],
            CreatedAt);

        Assert.Equal(2, order.Items.Count);
        Assert.All(order.Items, item => Assert.Equal(order.Id, item.OrderId));
        Assert.All(order.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
        Assert.Equal(2, order.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void Create_TrimsProductNames()
    {
        var order = Order.Create(
            OrderFactory.DefaultCustomerId,
            [new NewOrderItem("  Mechanical Keyboard  ", 1, 10.00m)],
            CreatedAt);

        Assert.Equal("Mechanical Keyboard", order.Items[0].ProductName);
    }

    // The rule the test statement calls out explicitly: the total is the domain's
    // responsibility. It is asserted here, not in a handler test, which is the whole
    // point of computing it here.
    [Fact]
    public void TotalAmount_IsTheSumOfQuantityTimesUnitPriceAcrossAllItems()
    {
        var order = Order.Create(
            OrderFactory.DefaultCustomerId,
            [
                new NewOrderItem("Monitor", 2, 1_250.50m),  // 2501.00
                new NewOrderItem("Cable", 3, 19.90m),       //   59.70
                new NewOrderItem("Dock", 1, 449.99m),       //  449.99
            ],
            CreatedAt);

        Assert.Equal(3_010.69m, order.TotalAmount);
    }

    [Fact]
    public void TotalAmount_OfASingleLine_MatchesThatLineTotal()
    {
        var order = OrderFactory.Pending(items: new NewOrderItem("Headset", 4, 89.90m));

        Assert.Equal(359.60m, order.TotalAmount);
        Assert.Equal(359.60m, order.Items[0].LineTotal);
    }

    [Fact]
    public void Create_WithNoItems_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() =>
            Order.Create(OrderFactory.DefaultCustomerId, [], CreatedAt));

        Assert.Contains("at least one item", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithNullItems_IsRejected() =>
        Assert.Throws<ArgumentNullException>(() =>
            Order.Create(OrderFactory.DefaultCustomerId, null!, CreatedAt));

    [Fact]
    public void Create_WithoutACustomer_IsRejected() =>
        Assert.Throws<DomainException>(() =>
            Order.Create(Guid.Empty, [new NewOrderItem("Mouse", 1, 10m)], CreatedAt));

    [Fact]
    public void Cancel_OnAPendingOrder_Succeeds()
    {
        var order = OrderFactory.Pending();

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    // "Apenas pedidos com status Pending podem ser cancelados" - proven from both
    // non-pending states, not just the obvious one.
    [Fact]
    public void Cancel_OnAnAlreadyCancelledOrder_IsRejected()
    {
        var order = OrderFactory.Cancelled();

        var exception = Assert.Throws<InvalidOrderStateException>(order.Cancel);

        Assert.Contains("Cancelled", exception.Message, StringComparison.Ordinal);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_OnAConfirmedOrder_IsRejected()
    {
        var order = OrderFactory.Confirmed();

        Assert.Throws<InvalidOrderStateException>(order.Cancel);

        // The failed transition must leave the aggregate exactly as it was.
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Confirm_OnAPendingOrder_Succeeds()
    {
        var order = OrderFactory.Pending();

        order.Confirm();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Confirm_OnACancelledOrder_IsRejected()
    {
        var order = OrderFactory.Cancelled();

        Assert.Throws<InvalidOrderStateException>(order.Confirm);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.01")]
    public void Create_WithANonPositiveUnitPrice_IsRejected(string unitPrice)
    {
        var price = decimal.Parse(unitPrice, CultureInfo.InvariantCulture);

        var exception = Assert.Throws<DomainException>(() =>
            Order.Create(OrderFactory.DefaultCustomerId, [new NewOrderItem("Mouse", 1, price)], CreatedAt));

        Assert.Contains("unit price", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithANonPositiveQuantity_IsRejected(int quantity)
    {
        var exception = Assert.Throws<DomainException>(() =>
            Order.Create(OrderFactory.DefaultCustomerId, [new NewOrderItem("Mouse", quantity, 10m)], CreatedAt));

        Assert.Contains("quantity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithABlankProductName_IsRejected(string productName) =>
        Assert.Throws<DomainException>(() =>
            Order.Create(OrderFactory.DefaultCustomerId, [new NewOrderItem(productName, 1, 10m)], CreatedAt));

    [Fact]
    public void Create_WithAnOverlongProductName_IsRejected()
    {
        var productName = new string('x', OrderItem.MaxProductNameLength + 1);

        Assert.Throws<DomainException>(() =>
            Order.Create(OrderFactory.DefaultCustomerId, [new NewOrderItem(productName, 1, 10m)], CreatedAt));
    }

    // One invalid line must reject the whole order rather than quietly dropping the
    // bad line and charging the customer for the rest.
    [Fact]
    public void Create_WithOneInvalidItemAmongValidOnes_RejectsTheWholeOrder() =>
        Assert.Throws<DomainException>(() =>
            Order.Create(
                OrderFactory.DefaultCustomerId,
                [new NewOrderItem("Valid", 1, 10m), new NewOrderItem("Invalid", 0, 10m)],
                CreatedAt));
}
