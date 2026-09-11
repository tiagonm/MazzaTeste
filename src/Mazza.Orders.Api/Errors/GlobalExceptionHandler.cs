using FluentValidation;
using Mazza.Orders.Application.Common.Exceptions;
using Mazza.Orders.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mazza.Orders.Api.Errors;

/// <summary>
/// Translates exceptions thrown anywhere below the API into RFC 9457 problem
/// responses.
///
/// This is the single place in the solution that knows about HTTP status codes for
/// failures. Handlers and the domain throw meaningful exceptions and stay unaware of
/// the transport, and no endpoint needs a try/catch - which is what keeps the endpoint
/// bodies down to two lines each.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = Map(exception);

        var statusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        // Expected failures (a 404, a rejected password) are noise at error level;
        // only genuine faults deserve to page someone.
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            ApiLog.UnhandledException(_logger, exception);
        }
        else
        {
            ApiLog.RequestFailed(_logger, statusCode, problem.Title ?? "Request failed.");
        }

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        // FluentValidation ran in the MediatR pipeline: report every field at once so
        // the caller can fix the whole payload in one go.
        ValidationException validation => new ValidationProblemDetails(ToErrorDictionary(validation))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        },

        AuthenticationFailedException => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication failed.",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        },

        NotFoundException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found.",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        },

        // A state transition the aggregate refused - the request was well formed, but
        // the resource is not in a state where it can be honoured. That is 409, not 400:
        // the caller could retry the identical request later and succeed.
        InvalidOrderStateException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The order is not in a state that allows this operation.",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        },

        // Any other broken invariant: syntactically valid, semantically unacceptable.
        DomainException => new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "The request violates a business rule.",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
        },

        // Client hung up. Nothing to report and nobody left to report it to.
        OperationCanceledException => new ProblemDetails
        {
            Status = StatusCodes.Status499ClientClosedRequest,
            Title = "The request was cancelled.",
        },

        // Anything unrecognised is a bug. The exception is logged in full above; the
        // response says nothing about it, because stack traces and connection strings
        // are not the client's business.
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        },
    };

    private static Dictionary<string, string[]> ToErrorDictionary(ValidationException exception) =>
        exception.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
}

/// <summary>
/// Source-generated logging delegates, so the status code is not boxed on every
/// failed request.
/// </summary>
internal static partial class ApiLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing the request.")]
    public static partial void UnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Request failed with {StatusCode}: {ErrorTitle}")]
    public static partial void RequestFailed(ILogger logger, int statusCode, string errorTitle);
}
