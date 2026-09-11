using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Mazza.Orders.Application.Common.Behaviors;

/// <summary>
/// Logs every command and query flowing through MediatR, with its elapsed time and
/// outcome.
///
/// Two decisions worth calling out:
///
/// 1. It depends on <see cref="ILogger{T}"/>, not on Serilog. The Application layer
///    must not know which logging library the host picked; Serilog is registered as
///    the provider in the API's composition root, which is what turns these calls
///    into structured events. Replacing Serilog would not touch this file.
///
/// 2. It logs the request <em>type</em>, never the request <em>payload</em>.
///    A behaviour that dumps the whole request would write
///    <c>LoginCommand.Password</c> to the log in plain text on every sign-in.
///    The type name plus the timing is what is actually useful for diagnostics.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        PipelineLog.Handling(_logger, requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);

            stopwatch.Stop();
            PipelineLog.Handled(_logger, requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            // Log and rethrow: the pipeline observes the failure, but the API's
            // exception handler remains the single place that maps it to a response.
            PipelineLog.Failed(_logger, requestName, stopwatch.ElapsedMilliseconds, exception);
            throw;
        }
    }
}

/// <summary>
/// Source-generated logging delegates. These avoid boxing the arguments and
/// re-parsing the message template on every request, which matters here because
/// this runs for every single command and query.
/// </summary>
internal static partial class PipelineLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    public static partial void Handling(ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Handled {RequestName} in {ElapsedMilliseconds} ms")]
    public static partial void Handled(ILogger logger, string requestName, long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "{RequestName} failed after {ElapsedMilliseconds} ms")]
    public static partial void Failed(
        ILogger logger,
        string requestName,
        long elapsedMilliseconds,
        Exception exception);
}
