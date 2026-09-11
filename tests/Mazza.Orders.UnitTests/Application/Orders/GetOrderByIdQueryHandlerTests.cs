using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Common.Exceptions;
using Mazza.Orders.Application.Orders.Queries.GetOrderById;
using Mazza.Orders.Domain.Orders;
using Mazza.Orders.UnitTests.TestSupport;
using NSubstitute;

namespace Mazza.Orders.UnitTests.Application.Orders;

/// <summary>
/// Tests for <see cref="GetOrderByIdQueryHandler"/>.
/// </summary>
public sealed class GetOrderByIdQueryHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly GetOrderByIdQueryHandler _handler;

    public GetOrderByIdQueryHandlerTests()
    {
        _handler = new GetOrderByIdQueryHandler(_orders);
    }

    [Fact]
    public async Task Handle_WhenTheOrderExists_ReturnsItWithEveryLine()
    {
        var order = OrderFactory.Pending(
            items:
            [
                new NewOrderItem("Monitor", 2, 1_250.50m),
                new NewOrderItem("Cable", 3, 19.90m),
            ]);

        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        Assert.Equal(order.Id, result.Id);
        Assert.Equal(order.CustomerId, result.CustomerId);
        Assert.Equal(nameof(OrderStatus.Pending), result.Status);
        Assert.Equal(2_560.70m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Handle_ProjectsItemFieldsFaithfully()
    {
        var order = OrderFactory.Pending(items: new NewOrderItem("Mechanical Keyboard", 3, 249.90m));
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Mechanical Keyboard", item.ProductName);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(249.90m, item.UnitPrice);
        Assert.Equal(749.70m, item.LineTotal);
    }

    [Fact]
    public async Task Handle_WhenTheOrderDoesNotExist_ThrowsNotFound()
    {
        var missingId = Guid.CreateVersion7();
        _orders.FindByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((Order?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new GetOrderByIdQuery(missingId), CancellationToken.None));

        Assert.Contains(missingId.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_ReportsACancelledOrderAsCancelled()
    {
        var order = OrderFactory.Cancelled();
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        Assert.Equal(nameof(OrderStatus.Cancelled), result.Status);
    }
}
