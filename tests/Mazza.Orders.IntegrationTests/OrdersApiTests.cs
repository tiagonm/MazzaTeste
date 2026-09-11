using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mazza.Orders.IntegrationTests.Infrastructure;

namespace Mazza.Orders.IntegrationTests;

/// <summary>
/// End-to-end tests over the real HTTP pipeline.
///
/// These cover what unit tests structurally cannot: that JWT validation is actually
/// wired up, that the exception handler produces the status codes the endpoints
/// advertise, that EF maps the aggregate well enough to round-trip through SQLite,
/// and that the migrations really do run at startup.
/// </summary>
public sealed class OrdersApiTests : IClassFixture<OrdersApiFactory>
{
    private const string LoginRoute = "/auth/login";
    private const string OrdersRoute = "/api/orders";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrdersApiFactory _factory;

    public OrdersApiTests(OrdersApiFactory factory)
    {
        _factory = factory;
    }

    // ----------------------------------------------------------------- auth

    [Fact]
    public async Task Login_WithTheConfiguredCredentials_ReturnsAUsableToken()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LoginRoute,
            new { email = "dev@martech.com", password = "Senha@123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);

        Assert.NotNull(token);
        Assert.Equal("Bearer", token.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        // A JWT is three dot-separated segments; anything else is not a token.
        Assert.Equal(3, token.AccessToken.Split('.').Length);
        Assert.True(token.ExpiresInSeconds > 0);
    }

    [Fact]
    public async Task Login_WithAWrongPassword_Returns401()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LoginRoute,
            new { email = "dev@martech.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAnUnknownUser_Returns401()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LoginRoute,
            new { email = "nobody@martech.com", password = "Senha@123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAMissingPassword_Returns400WithTheOffendingField()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LoginRoute,
            new { email = "dev@martech.com", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);

        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Password"));
    }

    // -------------------------------------------------------- authorisation

    [Theory]
    [InlineData("GET", OrdersRoute)]
    [InlineData("POST", OrdersRoute)]
    [InlineData("GET", OrdersRoute + "/0192d4a1-0000-7000-8000-000000000001")]
    [InlineData("PATCH", OrdersRoute + "/0192d4a1-0000-7000-8000-000000000001/cancel")]
    public async Task EveryOrderEndpointRequiresAToken(string method, string route)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AGarbageTokenIsRejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        using var response = await client.GetAsync(OrdersRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --------------------------------------------------------------- orders

    [Fact]
    public async Task CreateOrder_Returns201WithTheDomainComputedTotalAndALocationHeader()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync(OrdersRoute, new
        {
            customerId = Guid.CreateVersion7(),
            items = new[]
            {
                new { productName = "Monitor", quantity = 2, unitPrice = 1_250.50m },
                new { productName = "Cable", quantity = 3, unitPrice = 19.90m },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        Assert.NotNull(order);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(2_560.70m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location?.ToString());
    }

    // The real round-trip check: an order written through EF into SQLite and read back
    // on a fresh request has to come out identical, money included.
    [Fact]
    public async Task CreateThenGetById_RoundTripsThroughSqliteUnchanged()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var customerId = Guid.CreateVersion7();

        var created = await CreateOrderAsync(client, customerId, ("Mechanical Keyboard", 3, 249.90m));

        using var response = await client.GetAsync($"{OrdersRoute}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fetched = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(customerId, fetched.CustomerId);
        Assert.Equal(749.70m, fetched.TotalAmount);

        var item = Assert.Single(fetched.Items);
        Assert.Equal("Mechanical Keyboard", item.ProductName);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(249.90m, item.UnitPrice);
        Assert.Equal(749.70m, item.LineTotal);
    }

    [Fact]
    public async Task GetById_ForAnUnknownOrder_Returns404()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"{OrdersRoute}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithNoItems_Returns400()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync(OrdersRoute, new
        {
            customerId = Guid.CreateVersion7(),
            items = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);

        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Items"));
    }

    [Fact]
    public async Task CreateOrder_WithAnInvalidItem_Returns400NamingEveryBrokenField()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync(OrdersRoute, new
        {
            customerId = Guid.CreateVersion7(),
            items = new[] { new { productName = "", quantity = 0, unitPrice = -5m } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);

        Assert.NotNull(problem);

        // One request, every problem reported - that is what the validation behaviour buys.
        Assert.True(
            problem.Errors.Count >= 3,
            $"Expected at least 3 invalid fields, got {problem.Errors.Count}.");
    }

    // --------------------------------------------------------------- cancel

    [Fact]
    public async Task Cancel_OnAPendingOrder_Returns200AndTheOrderStaysCancelled()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client, Guid.CreateVersion7(), ("Mouse", 1, 89.90m));

        using var response = await client.PatchAsync($"{OrdersRoute}/{created.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cancelled = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);

        // And it was actually persisted, not just reported back.
        var refetched = await client.GetFromJsonAsync<OrderResponse>(
            $"{OrdersRoute}/{created.Id}", JsonOptions);

        Assert.NotNull(refetched);
        Assert.Equal("Cancelled", refetched.Status);
    }

    // The business rule, end to end: 409 rather than 400, because the request was well
    // formed - the resource simply is not in a cancellable state.
    [Fact]
    public async Task Cancel_Twice_Returns409()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client, Guid.CreateVersion7(), ("Mouse", 1, 89.90m));

        using var first = await client.PatchAsync($"{OrdersRoute}/{created.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.PatchAsync($"{OrdersRoute}/{created.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Cancel_AnUnknownOrder_Returns404()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PatchAsync(
            $"{OrdersRoute}/{Guid.CreateVersion7()}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ----------------------------------------------------------- pagination

    [Fact]
    public async Task GetOrders_PaginatesAndNeverRepeatsAnOrderAcrossPages()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var customerId = Guid.CreateVersion7();

        var createdIds = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            var order = await CreateOrderAsync(client, customerId, ($"Item {index}", 1, 10m));
            createdIds.Add(order.Id);
        }

        var firstPage = await client.GetFromJsonAsync<PagedResponse<OrderSummaryResponse>>(
            $"{OrdersRoute}?page=1&pageSize=2", JsonOptions);

        var secondPage = await client.GetFromJsonAsync<PagedResponse<OrderSummaryResponse>>(
            $"{OrdersRoute}?page=2&pageSize=2", JsonOptions);

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.True(firstPage.TotalCount >= 5);
        Assert.True(firstPage.HasNextPage);
        Assert.False(firstPage.HasPreviousPage);
        Assert.True(secondPage.HasPreviousPage);

        // A stable sort is the whole reason CreatedAt is tie-broken by id: without it
        // an order could show up on both pages, or on neither.
        var firstIds = firstPage.Items.Select(item => item.Id).ToList();
        var secondIds = secondPage.Items.Select(item => item.Id).ToList();
        Assert.Empty(firstIds.Intersect(secondIds));

        // Newest first, so the most recently created order leads page one.
        Assert.Equal(createdIds[^1], firstIds[0]);
    }

    [Fact]
    public async Task GetOrders_SummarisesEachOrderWithItsTotalAndItemCount()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var created = await CreateOrderAsync(
            client,
            Guid.CreateVersion7(),
            ("Monitor", 2, 1_250.50m),
            ("Cable", 3, 19.90m));

        var page = await client.GetFromJsonAsync<PagedResponse<OrderSummaryResponse>>(
            $"{OrdersRoute}?page=1&pageSize=10", JsonOptions);

        Assert.NotNull(page);

        var summary = Assert.Single(page.Items, item => item.Id == created.Id);
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(2_560.70m, summary.TotalAmount);
    }

    [Theory]
    [InlineData("?page=0&pageSize=10")]
    [InlineData("?page=1&pageSize=0")]
    [InlineData("?page=1&pageSize=101")]
    public async Task GetOrders_WithInvalidPagination_Returns400(string queryString)
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(OrdersRoute + queryString);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithNoQueryString_FallsBackToTenPerPage()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var page = await client.GetFromJsonAsync<PagedResponse<OrderSummaryResponse>>(
            OrdersRoute, JsonOptions);

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
    }

    // ------------------------------------------------------------- plumbing

    [Fact]
    public async Task HealthEndpointIsAnonymousAndHealthy()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Proves the migrations ran at startup: without them there would be no Orders
    // table, and this query would fail instead of returning an empty page.
    [Fact]
    public async Task MigrationsAreAppliedOnStartup()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(OrdersRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LoginRoute,
            new { email = "dev@martech.com", password = "Senha@123" });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload!.AccessToken);

        return client;
    }

    private static async Task<OrderResponse> CreateOrderAsync(
        HttpClient client,
        Guid customerId,
        params (string ProductName, int Quantity, decimal UnitPrice)[] items)
    {
        using var response = await client.PostAsJsonAsync(OrdersRoute, new
        {
            customerId,
            items = items.Select(item => new
            {
                productName = item.ProductName,
                quantity = item.Quantity,
                unitPrice = item.UnitPrice,
            }),
        });

        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        return order!;
    }

    // Response shapes are declared here rather than deserialised into the Application
    // DTOs on purpose: renaming a DTO property is a breaking change to the HTTP
    // contract, and these tests should be what tells you so.
    private sealed record LoginResponse(
        string AccessToken,
        string TokenType,
        DateTimeOffset ExpiresAtUtc,
        int ExpiresInSeconds);

    private sealed record OrderResponse(
        Guid Id,
        Guid CustomerId,
        string Status,
        DateTime CreatedAt,
        decimal TotalAmount,
        IReadOnlyList<OrderItemResponse> Items);

    private sealed record OrderItemResponse(
        Guid Id,
        string ProductName,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);

    private sealed record OrderSummaryResponse(
        Guid Id,
        Guid CustomerId,
        string Status,
        DateTime CreatedAt,
        decimal TotalAmount,
        int ItemCount);

    private sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);

    private sealed record ValidationProblem(
        string? Title,
        int? Status,
        [property: JsonPropertyName("errors")] Dictionary<string, string[]> Errors);
}
