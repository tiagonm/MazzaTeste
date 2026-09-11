namespace Mazza.Orders.Application.Common.Abstractions.Identity;

/// <summary>
/// Mints an <see cref="AccessToken"/> for an authenticated user.
/// The signing algorithm, key and claim layout are Infrastructure concerns.
/// </summary>
public interface IAccessTokenFactory
{
    AccessToken Create(UserAccount user);
}
