using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mazza.Orders.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in-process for integration testing.
///
/// The only thing swapped out is the database file: everything else - the DI graph,
/// the MediatR pipeline, JWT validation, the exception handler, the real SQLite
/// provider and the real migrations - is exactly what runs in production. That is the
/// point of these tests; anything more mocked out would only be re-testing the unit
/// tests through a slower interface.
///
/// A file-backed SQLite database rather than <c>:memory:</c>, because an in-memory
/// SQLite database lives and dies with a single connection, while EF opens and closes
/// connections per operation. A throwaway file behaves like the real thing.
/// </summary>
public sealed class OrdersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"mazza-orders-tests-{Guid.CreateVersion7():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development reuses appsettings.Development.json, which keeps the noisy
        // OpenTelemetry console exporter out of the test output.
        builder.UseEnvironment("Development");

        // Each test class gets its own database, so classes can run in parallel
        // without seeing each other's orders.
        builder.UseSetting("ConnectionStrings:OrdersDatabase", $"Data Source={_databasePath}");
    }

    public Task InitializeAsync()
    {
        // Touching Server forces the host to build and start, which is what runs the
        // startup migration. Doing it here rather than lazily in the first test means
        // a schema problem fails setup instead of one arbitrary test.
        _ = Server;

        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();

        // Best effort: the host has released the file by now, but a stray lock should
        // not fail an otherwise passing test run.
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // Leave it to the operating system's temp cleanup.
        }
    }
}
