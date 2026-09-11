using Mazza.Orders.Application.Orders.Queries.GetOrders;

namespace Mazza.Orders.UnitTests.Application.Orders;

/// <summary>
/// Tests for <see cref="GetOrdersQueryValidator"/> - the guard rails on the
/// ?page=1&amp;pageSize=10 contract.
/// </summary>
public sealed class GetOrdersQueryValidatorTests
{
    private readonly GetOrdersQueryValidator _validator = new();

    [Fact]
    public void TheDefaultQueryPasses() =>
        Assert.True(_validator.Validate(new GetOrdersQuery()).IsValid);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(7, 50)]
    [InlineData(1, GetOrdersQuery.MaxPageSize)]
    public void ValidPaginationPasses(int page, int pageSize) =>
        Assert.True(_validator.Validate(new GetOrdersQuery(page, pageSize)).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APageBelowOneFails(int page)
    {
        var result = _validator.Validate(new GetOrdersQuery(page, 10));

        Assert.Contains(result.Errors, error => error.PropertyName == "Page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void APageSizeBelowOneFails(int pageSize)
    {
        var result = _validator.Validate(new GetOrdersQuery(1, pageSize));

        Assert.Contains(result.Errors, error => error.PropertyName == "PageSize");
    }

    // Rejected rather than clamped: an unbounded pageSize is a cheap way to make the
    // server materialise the whole table, and a caller asking for a million rows has
    // a bug worth telling them about.
    [Theory]
    [InlineData(101)]
    [InlineData(1_000_000)]
    public void APageSizeAboveTheCeilingFails(int pageSize)
    {
        var result = _validator.Validate(new GetOrdersQuery(1, pageSize));

        Assert.Contains(result.Errors, error => error.PropertyName == "PageSize");
    }
}
