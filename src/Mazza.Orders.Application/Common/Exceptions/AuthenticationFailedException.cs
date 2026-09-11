namespace Mazza.Orders.Application.Common.Exceptions;

/// <summary>
/// Credentials were rejected. Translated to HTTP 401 by the API's exception handler.
/// The message is intentionally generic so the response cannot be used to enumerate
/// valid accounts.
/// </summary>
public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException()
        : base("Invalid email or password.")
    {
    }
}
