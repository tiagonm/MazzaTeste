namespace Mazza.Orders.Domain.Common;

/// <summary>
/// Thrown when an attempt is made to put an aggregate into an invalid state.
/// These are the last line of defence: input is validated at the edge by
/// FluentValidation, but the aggregate still refuses to be corrupted by any
/// caller, including future code that forgets to validate.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
