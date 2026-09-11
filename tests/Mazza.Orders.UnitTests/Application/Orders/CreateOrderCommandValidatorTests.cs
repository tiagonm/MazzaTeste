using System.Globalization;
using Mazza.Orders.Application.Orders.Commands.CreateOrder;
using Mazza.Orders.Domain.Orders;
using Mazza.Orders.UnitTests.TestSupport;

namespace Mazza.Orders.UnitTests.Application.Orders;

/// <summary>
/// Tests for <see cref="CreateOrderCommandValidator"/>.
///
/// These cover the same rules as the domain tests, from the other side of the
/// boundary: the domain proves an invalid order cannot exist, and these prove the
/// caller is told which field was wrong instead of getting a bare failure.
/// </summary>
public sealed class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void AValidCommandPasses()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AnEmptyCustomerIdFails()
    {
        var result = _validator.Validate(new CreateOrderCommand(
            Guid.Empty,
            [new CreateOrderItemRequest("Keyboard", 1, 10m)]));

        Assert.Contains(result.Errors, error => error.PropertyName == "CustomerId");
    }

    [Fact]
    public void AnOrderWithNoItemsFails()
    {
        var result = _validator.Validate(new CreateOrderCommand(OrderFactory.DefaultCustomerId, []));

        Assert.Contains(result.Errors, error => error.PropertyName == "Items");
    }

    [Fact]
    public void AnOrderWithNullItemsFails()
    {
        var result = _validator.Validate(new CreateOrderCommand(OrderFactory.DefaultCustomerId, null!));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ANonPositiveQuantityFails(int quantity)
    {
        var result = _validator.Validate(WithItem(new CreateOrderItemRequest("Keyboard", quantity, 10m)));

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("Quantity", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("-50")]
    public void ANonPositiveUnitPriceFails(string unitPrice)
    {
        var price = decimal.Parse(unitPrice, CultureInfo.InvariantCulture);

        var result = _validator.Validate(WithItem(new CreateOrderItemRequest("Keyboard", 1, price)));

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("UnitPrice", StringComparison.Ordinal));
    }

    // Money is stored as decimal(18,2). Without this rule, 10.999 would be accepted
    // here and silently truncated on save, so the persisted total would disagree with
    // what the caller was told.
    [Theory]
    [InlineData("10.999")]
    [InlineData("0.001")]
    public void AUnitPriceWithMoreThanTwoDecimalPlacesFails(string unitPrice)
    {
        var price = decimal.Parse(unitPrice, CultureInfo.InvariantCulture);

        var result = _validator.Validate(WithItem(new CreateOrderItemRequest("Keyboard", 1, price)));

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("UnitPrice", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("10")]
    [InlineData("10.5")]
    [InlineData("10.50")]
    [InlineData("1999.99")]
    public void AUnitPriceWithinTwoDecimalPlacesPasses(string unitPrice)
    {
        var price = decimal.Parse(unitPrice, CultureInfo.InvariantCulture);

        var result = _validator.Validate(WithItem(new CreateOrderItemRequest("Keyboard", 1, price)));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankProductNameFails(string productName)
    {
        var result = _validator.Validate(WithItem(new CreateOrderItemRequest(productName, 1, 10m)));

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("ProductName", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOverlongProductNameFails()
    {
        var productName = new string('x', OrderItem.MaxProductNameLength + 1);

        var result = _validator.Validate(WithItem(new CreateOrderItemRequest(productName, 1, 10m)));

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("ProductName", StringComparison.Ordinal));
    }

    // The reason validation lives in a pipeline behaviour at all: the caller gets the
    // full list of problems in one response instead of discovering them one at a time.
    [Fact]
    public void EveryProblemIsReportedAtOnce()
    {
        var result = _validator.Validate(new CreateOrderCommand(
            Guid.Empty,
            [new CreateOrderItemRequest("", 0, -1m)]));

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 4, $"Expected at least 4 errors, got {result.Errors.Count}.");
    }

    [Fact]
    public void AnInvalidItemIsReportedWithItsIndex()
    {
        var result = _validator.Validate(new CreateOrderCommand(
            OrderFactory.DefaultCustomerId,
            [
                new CreateOrderItemRequest("Valid", 1, 10m),
                new CreateOrderItemRequest("Invalid", 0, 10m),
            ]));

        // "Items[1].Quantity" - the client needs to know which line was wrong.
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("[1]", StringComparison.Ordinal));
    }

    private static CreateOrderCommand ValidCommand() =>
        WithItem(new CreateOrderItemRequest("Keyboard", 2, 199.90m));

    private static CreateOrderCommand WithItem(CreateOrderItemRequest item) =>
        new(OrderFactory.DefaultCustomerId, [item]);
}
