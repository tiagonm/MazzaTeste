namespace Mazza.Orders.Api.Configuration;

/// <summary>
/// Switches for the OpenTelemetry pipeline, bound from the <c>Telemetry</c> section.
/// </summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Whether traces and metrics are printed to stdout.
    ///
    /// Behind a flag because the console exporter is genuinely loud - it dumps a
    /// metrics block on every collection interval, which drowns out the request logs
    /// it sits next to. On by default so the requirement is visible out of the box,
    /// and one setting away from quiet when it gets in the way.
    /// </summary>
    public bool ConsoleExporterEnabled { get; init; } = true;

    public string ServiceName { get; init; } = "Mazza.Orders.Api";
}
