using FluentValidation;

namespace Mazza.Orders.Application.Authentication.Commands.Login;

/// <summary>
/// Checks only that both fields were supplied.
///
/// No password-strength or email-format rules here: this endpoint verifies an existing
/// credential rather than creating one, and rules about what a valid password looks
/// like would only tell an attacker which guesses are worth making. Whether the
/// credential is correct is decided by <c>IUserAuthenticator</c>.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .WithMessage("Email is required.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
