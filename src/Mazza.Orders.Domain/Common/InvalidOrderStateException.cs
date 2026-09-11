namespace Mazza.Orders.Domain.Common;

/// <summary>
/// A specialisation of <see cref="DomainException"/> for illegal state transitions
/// (for example, cancelling an order that is no longer <c>Pending</c>).
///
/// It exists as a separate type so the API can translate it to HTTP 409 Conflict,
/// while a plain <see cref="DomainException"/> means "the request itself was wrong"
/// and becomes a 422. The domain does not know about HTTP - the API does the mapping.
/// </summary>
public sealed class InvalidOrderStateException : DomainException
{
    public InvalidOrderStateException(string message)
        : base(message)
    {
    }
}
