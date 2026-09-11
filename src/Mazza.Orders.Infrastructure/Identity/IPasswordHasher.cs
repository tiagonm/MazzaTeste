namespace Mazza.Orders.Infrastructure.Identity;

/// <summary>
/// Hashes and verifies passwords. Internal to Infrastructure: no layer above needs to
/// know that passwords are hashed at all, let alone how.
/// </summary>
internal interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash string, salt included.</summary>
    string Hash(string password);

    /// <summary>Verifies a candidate password against a hash produced by <see cref="Hash"/>.</summary>
    bool Verify(string password, string hash);
}
