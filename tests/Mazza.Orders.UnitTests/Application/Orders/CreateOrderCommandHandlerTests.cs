using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Orders.Commands.CreateOrder;
using Mazza.Orders.Domain.Common;
using Mazza.Orders.Domain.Orders;
using Mazza.Orders.UnitTests.TestSupport;
using NSubstitute;

namespace Mazza.Orders.UnitTests.Application.Orders;

/// <summary>
/// Tests for <see cref="CreateOrderCommandHandler"/>.
///
/// The repository and unit of work are substituted, so these run in milliseconds with
/// no database - which is only possible because the handler depends on the ports
/// declared in Application rather than on EF Core.
/// </summary>
public sealed class CreateOrderCommandHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FixedTimeProvider _timeProvider = new();
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _handler = new CreateOrderCommandHandler(_orders, _unitOfWork, _timeProvider);
    }

    [Fact]
    public async Task Handle_PersistsTheOrderAndCommitsExactlyOnce()
    {
        var command = new CreateOrderCommand(
            OrderFactory.DefaultCustomerId,
            [new CreateOrderItemRequest("Keyboard", 2, 199.90m)]);

        await _handler.Handle(command, CancellationToken.None);

        _orders.Received(1).Add(Arg.Any<Order>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsTheCreatedOrderWithTheTotalComputedByTheDomain()
    {
        var command = new CreateOrderCommand(
            OrderFactory.DefaultCustomerId,
            [
                new CreateOrderItemRequest("Monitor", 2, 1_250.50m),
                new CreateOrderItemRequest("Cable", 3, 19.90m),
            ]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(2_560.70m, result.TotalAmount);
        Assert.Equal(nameof(OrderStatus.Pending), result.Status);
        Assert.Equal(OrderFactory.DefaultCustomerId, result.CustomerId);
        Assert.NotEqual(Guid.Empty, result.Id);

        // Line totals travel in the response too, so a client never recomputes money.
        Assert.Collection(
            result.Items,
            item => Assert.Equal(2_501.00m, item.LineTotal),
            item => Assert.Equal(59.70m, item.LineTotal));
    }

    [Fact]
    public async Task Handle_StampsCreatedAtFromTheInjectedClock()
    {
        var command = new CreateOrderCommand(
            OrderFactory.DefaultCustomerId,
            [new CreateOrderItemRequest("Keyboard", 1, 100m)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        // Exact equality, which is only assertable because the handler takes a
        // TimeProvider instead of reading DateTime.UtcNow.
        Assert.Equal(FixedTimeProvider.DefaultNow.UtcDateTime, result.CreatedAt);
    }

    [Fact]
    public async Task Handle_PassesTheAggregateItselfToTheRepository()
    {
        Order? added = null;
        _orders.When(repository => repository.Add(Arg.Any<Order>()))
            .Do(call => added = call.Arg<Order>());

        var command = new CreateOrderCommand(
            OrderFactory.DefaultCustomerId,
            [new CreateOrderItemRequest("Keyboard", 3, 10m)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(result.Id, added.Id);
        Assert.Equal(OrderStatus.Pending, added.Status);
        Assert.Equal(30m, added.TotalAmount);
    }

    // Invalid input normally never reaches the handler - the validation behaviour
    // rejects it first. This asserts the second line of defence: even called directly,
    // the handler cannot produce an invalid order, and nothing is committed.
    [Fact]
    public async Task Handle_WithNoItems_FailsInTheDomainAndCommitsNothing()
    {
        var command = new CreateOrderCommand(OrderFactory.DefaultCustomerId, []);

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _orders.DidNotReceive().Add(Arg.Any<Order>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAnInvalidItem_CommitsNothing()
    {
        var command = new CreateOrderCommand(
            OrderFactory.DefaultCustomerId,
            [new CreateOrderItemRequest("Keyboard", 0, 100m)]);

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
