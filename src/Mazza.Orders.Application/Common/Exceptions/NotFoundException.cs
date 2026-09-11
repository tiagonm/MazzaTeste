namespace Mazza.Orders.Application.Common.Exceptions;

/// <summary>
/// The requested aggregate does not exist. Translated to HTTP 404 by the API's
/// exception handler - the handlers themselves stay free of HTTP concepts.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public static NotFoundException ForOrder(Guid orderId) =>
        new($"Order '{orderId}' was not found.");
}
