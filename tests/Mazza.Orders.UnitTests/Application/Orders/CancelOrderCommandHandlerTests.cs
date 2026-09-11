using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Common.Exceptions;
using Mazza.Orders.Application.Orders.Commands.CancelOrder;
using Mazza.Orders.Domain.Common;
using Mazza.Orders.Domain.Orders;
using Mazza.Orders.UnitTests.TestSupport;
using NSubstitute;

namespace Mazza.Orders.UnitTests.Application.Orders;

/// <summary>
/// Tests for <see cref="CancelOrderCommandHandler"/>.
/// </summary>
public sealed class CancelOrderCommandHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelOrderCommandHandler _handler;

    public CancelOrderCommandHandlerTests()
    {
        _handler = new CancelOrderCommandHandler(_orders, _unitOfWork);
    }

    [Fact]
    public async Task Handle_OnAPendingOrder_CancelsItAndCommits()
    {
        var order = OrderFactory.Pending();
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        Assert.Equal(nameof(OrderStatus.Cancelled), result.Status);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsTheOrderWithItsItemsAndTotalIntact()
    {
        var order = OrderFactory.Pending(items: new NewOrderItem("Monitor", 2, 999.99m));
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        // Cancelling changes the status, not the money already recorded.
        Assert.Equal(1_999.98m, result.TotalAmount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Handle_WhenTheOrderDoesNotExist_ThrowsNotFoundAndCommitsNothing()
    {
        var missingId = Guid.CreateVersion7();
        _orders.FindByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((Order?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new CancelOrderCommand(missingId), CancellationToken.None));

        Assert.Contains(missingId.ToString(), exception.Message, StringComparison.Ordinal);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // The interesting case: the handler does not check the status itself, so this is
    // really asserting that it lets the aggregate refuse - and that a refused
    // transition is never committed.
    [Fact]
    public async Task Handle_OnAnAlreadyCancelledOrder_ThrowsInvalidStateAndCommitsNothing()
    {
        var order = OrderFactory.Cancelled();
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        await Assert.ThrowsAsync<InvalidOrderStateException>(() =>
            _handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnAConfirmedOrder_ThrowsInvalidStateAndCommitsNothing()
    {
        var order = OrderFactory.Confirmed();
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        await Assert.ThrowsAsync<InvalidOrderStateException>(() =>
            _handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None));

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PropagatesTheCancellationTokenToThePersistenceLayer()
    {
        using var cancellation = new CancellationTokenSource();
        var order = OrderFactory.Pending();
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        await _handler.Handle(new CancelOrderCommand(order.Id), cancellation.Token);

        // A dropped token means a cancelled request keeps holding a database
        // connection, so it is worth asserting rather than assuming.
        await _orders.Received(1).FindByIdAsync(order.Id, cancellation.Token);
        await _unitOfWork.Received(1).SaveChangesAsync(cancellation.Token);
    }
}
