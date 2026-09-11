using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Mazza.Orders.Application.Common.Behaviors;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for the incoming request before
/// the handler is reached.
///
/// Putting validation in the pipeline rather than at the top of each handler means a
/// new command is validated the moment its validator is written - nobody has to
/// remember to call it - and handlers can assume their input is already well formed.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Requests without a validator (queries by id, for instance) pay nothing here.
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        // All failures are reported at once so the caller can fix the whole payload
        // in a single round trip instead of discovering one problem per attempt.
        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
