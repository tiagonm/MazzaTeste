using Mazza.Orders.Domain.Orders;

namespace Mazza.Orders.Application.Common.Abstractions.Persistence;

/// <summary>
/// Persistence port for the Order aggregate, declared by the layer that consumes it
/// (Application) and implemented by Infrastructure - so the dependency arrow points
/// inwards and handlers can be unit tested without a database.
///
/// Deliberately NOT an <c>IRepository&lt;T&gt;</c>: a generic repository would expose
/// <c>IQueryable</c>/predicate plumbing to the handlers, leak EF Core semantics through
/// the abstraction and let any caller query any aggregate any way it likes. These four
/// methods are the complete set of persistence operations this use case actually needs,
/// each one named after the intent behind it.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Registers a new order to be persisted on the next
    /// <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    void Add(Order order);

    /// <summary>
    /// Loads a single order with its items, or <c>null</c> when it does not exist.
    /// Tracked, because callers mutate the result (e.g. cancelling).
    /// </summary>
    Task<Order?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads one page of orders, newest first, together with the total row count
    /// needed to build the pagination envelope.
    /// </summary>
    Task<OrderPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
}
