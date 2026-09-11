using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mazza.Orders.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations at startup.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Brings the database up to the latest migration.
    ///
    /// Called explicitly from <c>Program.cs</c> before the host starts serving,
    /// rather than from an <c>IHostedService</c>: hosted services registered after the
    /// web host start <em>after</em> Kestrel is already accepting connections, which
    /// would leave a window where requests hit a schema that does not exist yet.
    ///
    /// <c>Migrate</c>, not <c>EnsureCreated</c> - the latter skips the migrations
    /// history table entirely and leaves the database unable to be upgraded later.
    ///
    /// Worth being explicit about the trade-off: migrating on startup is right for a
    /// single-instance SQLite deployment like this one. With several replicas against
    /// a shared database it becomes a race, and migrations belong in a deployment step
    /// instead.
    /// </summary>
    public static async Task ApplyMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        await context.Database.MigrateAsync(cancellationToken);
    }
}
