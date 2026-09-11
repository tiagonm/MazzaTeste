using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mazza.Orders.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the <c>dotnet ef</c> tooling.
///
/// Without it, the tooling has to boot the API's host to find a DbContext, which
/// means every <c>migrations add</c> depends on the whole composition root - and on
/// <c>Program.cs</c> letting the tooling's sentinel exception escape its try/catch.
/// An explicit factory makes migrations a self-contained operation on the
/// Infrastructure project: no startup project, no configuration, no surprises.
///
/// This is only ever used by the CLI. At runtime the context comes from DI with the
/// real connection string.
/// </summary>
internal sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            // Scaffolding only reads the model, never this file - the provider is what
            // matters, because it decides the generated SQL dialect.
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new OrdersDbContext(options);
    }
}
