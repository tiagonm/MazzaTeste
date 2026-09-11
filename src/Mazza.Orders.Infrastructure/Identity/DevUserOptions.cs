using System.ComponentModel.DataAnnotations;

namespace Mazza.Orders.Infrastructure.Identity;

/// <summary>
/// The single hard-coded account the test asks for, bound from the
/// <c>Auth:DevUser</c> configuration section.
///
/// Keeping it in configuration rather than as a literal in code means the reviewer can
/// see exactly what the credentials are, and that replacing this with a real user
/// store later is a change to one Infrastructure class - nothing above it moves.
/// </summary>
public sealed class DevUserOptions
{
    public const string SectionName = "Auth:DevUser";

    [Required]
    public Guid Id { get; init; }

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
