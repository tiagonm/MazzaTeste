namespace Mazza.Orders.Application.Common.Abstractions.Persistence;

/// <summary>
/// Commits the changes tracked during a single request as one transaction.
///
/// Kept separate from <see cref="IOrderRepository"/> on purpose: the repository
/// answers "how do I reach this aggregate", the unit of work answers "when does the
/// work become durable". Splitting them keeps the commit decision in the handler,
/// which is the only place that knows a use case has finished.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
