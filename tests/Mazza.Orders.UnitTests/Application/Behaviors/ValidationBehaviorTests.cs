using FluentValidation;
using FluentValidation.Results;
using Mazza.Orders.Application.Common.Behaviors;
using MediatR;

namespace Mazza.Orders.UnitTests.Application.Behaviors;

/// <summary>
/// Tests for <see cref="ValidationBehavior{TRequest,TResponse}"/>.
///
/// Worth testing on its own: it is the single piece of code standing between a
/// malformed request and every handler in the application, so "did the handler
/// actually not run" is the assertion that matters.
/// </summary>
public sealed class ValidationBehaviorTests
{
    private sealed record TestRequest(string Name) : IRequest<string>;

    private sealed class AlwaysFailsValidator : AbstractValidator<TestRequest>
    {
        public AlwaysFailsValidator() =>
            RuleFor(request => request.Name).NotEmpty().WithMessage("Name is required.");
    }

    private sealed class SecondFailingValidator : AbstractValidator<TestRequest>
    {
        public SecondFailingValidator() =>
            RuleFor(request => request.Name).MinimumLength(5).WithMessage("Name is too short.");
    }

    [Fact]
    public async Task WithNoValidators_TheHandlerRuns()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);
        var handlerRan = false;

        var result = await behavior.Handle(
            new TestRequest("anything"),
            _ =>
            {
                handlerRan = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None);

        Assert.True(handlerRan);
        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task WhenValidationPasses_TheHandlerRuns()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new AlwaysFailsValidator()]);

        var result = await behavior.Handle(
            new TestRequest("valid"),
            _ => Task.FromResult("handled"),
            CancellationToken.None);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task WhenValidationFails_TheHandlerNeverRuns()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new AlwaysFailsValidator()]);
        var handlerRan = false;

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new TestRequest(string.Empty),
            _ =>
            {
                handlerRan = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None));

        // The whole point: an invalid command must not reach the domain at all.
        Assert.False(handlerRan);
    }

    [Fact]
    public async Task FailuresFromEveryValidatorAreCollectedIntoOneException()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(
            [new AlwaysFailsValidator(), new SecondFailingValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new TestRequest(string.Empty),
            _ => Task.FromResult("handled"),
            CancellationToken.None));

        // Two validators, two problems, one response - not "fix this, try again, fix
        // the next one".
        var messages = exception.Errors.Select(error => error.ErrorMessage).ToList();
        Assert.Contains("Name is required.", messages, StringComparer.Ordinal);
        Assert.Contains("Name is too short.", messages, StringComparer.Ordinal);
    }

    [Fact]
    public async Task TheReportedFailuresCarryThePropertyNameTheApiNeeds()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new AlwaysFailsValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new TestRequest(string.Empty),
            _ => Task.FromResult("handled"),
            CancellationToken.None));

        var failure = Assert.Single(exception.Errors);
        Assert.Equal(nameof(TestRequest.Name), failure.PropertyName);
    }

    [Fact]
    public async Task TheCancellationTokenReachesTheHandler()
    {
        using var cancellation = new CancellationTokenSource();
        var behavior = new ValidationBehavior<TestRequest, string>([new AlwaysFailsValidator()]);
        CancellationToken observed = default;

        await behavior.Handle(
            new TestRequest("valid"),
            token =>
            {
                observed = token;
                return Task.FromResult("handled");
            },
            cancellation.Token);

        Assert.Equal(cancellation.Token, observed);
    }

    // Guards a detail that is easy to get wrong: ValidationResult with no errors must
    // not be turned into an exception.
    [Fact]
    public void AnEmptyValidationResultIsConsideredValid() =>
        Assert.True(new ValidationResult().IsValid);
}
