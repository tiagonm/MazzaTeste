using Mazza.Orders.Api.Errors;
using Mazza.Orders.Api.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Mazza.Orders.Api.Configuration;

/// <summary>
/// Registers the API layer's own concerns: error translation, the OpenAPI document,
/// health reporting and telemetry.
///
/// Grouped here so <c>Program.cs</c> reads as a list of intentions
/// (<c>AddApplication</c>, <c>AddInfrastructure</c>, <c>AddApiServices</c>) rather
/// than eighty lines of wiring.
/// </summary>
internal static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
        });

        // Liveness only, and that is enough here: the database is a local SQLite file
        // and migrations run before the host starts serving, so a process that is up
        // has a schema that is ready. A shared/remote database would justify adding a
        // readiness probe that actually opens a connection.
        services.AddHealthChecks();

        services.AddApiTelemetry(configuration);

        return services;
    }

    private static IServiceCollection AddApiTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var telemetry = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>()
            ?? new TelemetryOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: telemetry.ServiceName,
                serviceVersion: typeof(ApiServiceCollectionExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (telemetry.ConsoleExporterEnabled)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();

                if (telemetry.ConsoleExporterEnabled)
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }
}
