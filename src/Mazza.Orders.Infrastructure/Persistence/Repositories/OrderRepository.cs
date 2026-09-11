using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Mazza.Orders.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IOrderRepository"/>.
///
/// This is the only file in the solution allowed to know that orders live in a
/// relational database. It contains no business rules - it loads and stores
/// aggregates, and nothing more.
/// </summary>
internal sealed class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public void Add(Order order) => _context.Orders.Add(order);

    public Task<Order?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        _context.Orders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public async Task<OrderPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        // Read-only path, so tracking snapshots would be pure overhead.
        var query = _context.Orders.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(order => order.Items)
            // CreatedAt alone is not a total order - two orders placed in the same
            // tick could swap places between pages and one of them would never be
            // returned. Id is a UUID v7, so it is monotonic and breaks the tie in
            // the same direction.
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OrderPage(orders, totalCount);
    }
}
