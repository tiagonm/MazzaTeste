using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Mazza.Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the orders database.
///
/// It implements <see cref="IUnitOfWork"/> directly instead of being wrapped in an
/// <c>EfUnitOfWork</c> class: a DbContext already <em>is</em> a unit of work, and a
/// wrapper whose only body is <c>=> _context.SaveChangesAsync()</c> would add a file
/// without adding a seam. The interface is what keeps Application from seeing this
/// type at all.
/// </summary>
public sealed class OrdersDbContext : DbContext, IUnitOfWork
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Only the aggregate root gets a DbSet. <c>OrderItem</c> is reachable exclusively
    /// through <c>Order</c>, which is precisely the boundary the aggregate defines -
    /// exposing <c>DbSet&lt;OrderItem&gt;</c> would invite code to edit a line without
    /// going through the order that owns it.
    /// </summary>
    public DbSet<Order> Orders => Set<Order>();

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this assembly, so adding a
        // configuration file is all it takes to register it.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
