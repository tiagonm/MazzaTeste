using FluentValidation;
using Mazza.Orders.Domain.Orders;

namespace Mazza.Orders.Application.Orders.Commands.CreateOrder;

/// <summary>
/// Shape and range checks for <see cref="CreateOrderCommand"/>, run by the MediatR
/// validation behaviour before the handler executes.
///
/// This intentionally overlaps with the guards inside the Order aggregate, and the
/// duplication earns its keep: the validator exists to give the caller a complete,
/// field-by-field 400 response, while the aggregate's guards exist to make the
/// invariant impossible to break from any other entry point. Neither one can be
/// deleted in favour of the other without losing something.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    private const int CurrencyScale = 2;

    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required.");

        RuleFor(command => command.Items)
            .NotNull()
            .NotEmpty()
            .WithMessage("An order must have at least one item.");

        RuleForEach(command => command.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductName)
                    .NotEmpty()
                    .WithMessage("ProductName is required.")
                    .MaximumLength(OrderItem.MaxProductNameLength);

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be greater than zero.");

                item.RuleFor(i => i.UnitPrice)
                    .GreaterThan(0m)
                    .WithMessage("UnitPrice must be greater than zero.")
                    .Must(HasCurrencyScale)
                    .WithMessage($"UnitPrice cannot have more than {CurrencyScale} decimal places.");
            });
    }

    /// <summary>
    /// Rejects prices such as 10.999 up front. Money is stored as decimal(18,2), so
    /// without this check the third decimal would be silently truncated on save and
    /// the persisted total would not match what the caller asked for.
    /// </summary>
    private static bool HasCurrencyScale(decimal value) =>
        decimal.Round(value, CurrencyScale) == value;
}
