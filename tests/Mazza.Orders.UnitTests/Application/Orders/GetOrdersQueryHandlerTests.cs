using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Application.Orders.Queries.GetOrders;
using Mazza.Orders.Domain.Orders;
using Mazza.Orders.UnitTests.TestSupport;
using NSubstitute;

namespace Mazza.Orders.UnitTests.Application.Orders;

/// <summary>
/// Tests for <see cref="GetOrdersQueryHandler"/>: the projection, and the pagination
/// envelope it builds around the page returned by the repository.
/// </summary>
public sealed class GetOrdersQueryHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly GetOrdersQueryHandler _handler;

    public GetOrdersQueryHandlerTests()
    {
        _handler = new GetOrdersQueryHandler(_orders);
    }

    [Fact]
    public async Task Handle_ForwardsPageAndPageSizeToTheRepository()
    {
        _orders.GetPageAsync(3, 25, Arg.Any<CancellationToken>())
            .Returns(new OrderPage([], 0));

        await _handler.Handle(new GetOrdersQuery(3, 25), CancellationToken.None);

        await _orders.Received(1).GetPageAsync(3, 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProjectsEachOrderIntoASummaryWithItsTotalAndItemCount()
    {
        var order = OrderFactory.Pending(
            items:
            [
                new NewOrderItem("Monitor", 2, 1_250.50m),
                new NewOrderItem("Cable", 3, 19.90m),
            ]);

        _orders.GetPageAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new OrderPage([order], 1));

        var result = await _handler.Handle(new GetOrdersQuery(1, 10), CancellationToken.None);

        var summary = Assert.Single(result.Items);
        Assert.Equal(order.Id, summary.Id);
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(2_560.70m, summary.TotalAmount);
        Assert.Equal(nameof(OrderStatus.Pending), summary.Status);
    }

    [Fact]
    public async Task Handle_ReportsTheTotalCountFromTheRepositoryNotThePageLength()
    {
        var pageOfTwo = new[] { OrderFactory.Pending(), OrderFactory.Pending() };

        _orders.GetPageAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(new OrderPage(pageOfTwo, TotalCount: 57));

        var result = await _handler.Handle(new GetOrdersQuery(1, 2), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(57, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Theory]
    [InlineData(1, 10, 0, 0, false, false)]
    [InlineData(1, 10, 10, 1, false, false)]
    [InlineData(1, 10, 11, 2, false, true)]
    [InlineData(2, 10, 25, 3, true, true)]
    [InlineData(3, 10, 25, 3, true, false)]
    public async Task Handle_ComputesThePaginationMetadata(
        int page,
        int pageSize,
        int totalCount,
        int expectedTotalPages,
        bool expectedHasPrevious,
        bool expectedHasNext)
    {
        _orders.GetPageAsync(page, pageSize, Arg.Any<CancellationToken>())
            .Returns(new OrderPage([], totalCount));

        var result = await _handler.Handle(new GetOrdersQuery(page, pageSize), CancellationToken.None);

        // A partial last page still counts as a page: 11 rows at 10 per page is two.
        Assert.Equal(expectedTotalPages, result.TotalPages);
        Assert.Equal(expectedHasPrevious, result.HasPreviousPage);
        Assert.Equal(expectedHasNext, result.HasNextPage);
    }

    [Fact]
    public async Task Handle_WithNoOrders_ReturnsAnEmptyPageRatherThanFailing()
    {
        _orders.GetPageAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new OrderPage([], 0));

        var result = await _handler.Handle(new GetOrdersQuery(), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_UsesTenPerPageWhenTheCallerDoesNotSayOtherwise()
    {
        _orders.GetPageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new OrderPage([], 0));

        await _handler.Handle(new GetOrdersQuery(), CancellationToken.None);

        // Matches the ?page=1&pageSize=10 contract in the test statement.
        await _orders.Received(1).GetPageAsync(1, 10, Arg.Any<CancellationToken>());
    }
}
