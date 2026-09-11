using FluentValidation;

namespace Mazza.Orders.Application.Orders.Queries.GetOrders;

/// <summary>
/// Keeps pagination arguments inside sane bounds. Invalid paging is a caller mistake,
/// so it produces a 400 rather than being silently clamped - a request for page 0 is
/// much more likely to be a bug in the caller than an intent to see page 1.
/// </summary>
public sealed class GetOrdersQueryValidator : AbstractValidator<GetOrdersQuery>
{
    public GetOrdersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be greater than or equal to 1.")
            .LessThanOrEqualTo(GetOrdersQuery.MaxPageSize)
            .WithMessage($"PageSize cannot exceed {GetOrdersQuery.MaxPageSize}.");
    }
}
