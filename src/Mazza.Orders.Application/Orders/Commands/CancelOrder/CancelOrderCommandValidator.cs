using FluentValidation;

namespace Mazza.Orders.Application.Orders.Commands.CancelOrder;

/// <summary>
/// Rejects an empty id before a pointless database round trip.
///
/// Whether the order may actually be cancelled is <em>not</em> checked here: that
/// depends on the order's current status, which is a business rule and therefore
/// belongs to the aggregate, not to a validator.
/// </summary>
public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");
    }
}
