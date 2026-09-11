using System.Security.Cryptography;
using Mazza.Orders.Api.Configuration;
using Mazza.Orders.Api.Endpoints;
using Mazza.Orders.Application;
using Mazza.Orders.Infrastructure;
using Mazza.Orders.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger: active before the container exists, so a failure during
// configuration binding or startup is reported instead of vanishing.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replaces the bootstrap logger once configuration is available. ReadFrom.Services
    // lets registered enrichers participate; the settings themselves live in
    // appsettings.json so log levels are a deployment decision, not a rebuild.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Configuration.UseEphemeralJwtSigningKeyInDevelopment(builder.Environment);

    // One call per layer, each owning its own wiring. The order reads outside-in and
    // the dependency direction is enforced by the project references, not by comments.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);

    var app = builder.Build();

    // Schema first: applied before the host starts accepting connections, so no
    // request can arrive against a database that has not been migrated yet.
    await app.Services.ApplyMigrationsAsync();

    // Must be the first thing in the pipeline - it can only convert exceptions that
    // are thrown by the middleware registered after it.
    app.UseExceptionHandler();

    // One structured summary line per request (method, path, status, duration)
    // instead of the framework's several, and it participates in the same Serilog
    // sinks as everything else.
    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthenticationEndpoints();
    app.MapOrderEndpoints();

    app.MapHealthChecks("/health").AllowAnonymous();

    // The document is always served; the interactive UI only in development, where
    // exposing an API explorer costs nothing and helps.
    app.MapOpenApi();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference(options => options
            .WithTitle("Mazza Orders API")
            .WithTheme(ScalarTheme.BluePlanet));
    }

    await app.RunAsync();

    return 0;
}
catch (Exception exception)
{
    // A crash during startup - a bad connection string, a signing key under 32
    // characters - would otherwise leave nothing but a non-zero exit code behind.
    Log.Fatal(exception, "The API terminated unexpectedly during startup.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Development-only convenience for the JWT signing key.
/// </summary>
internal static class DevelopmentSigningKey
{
    /// <summary>
    /// Generates a random signing key when none is configured, but only in
    /// Development.
    ///
    /// The point is that there is no signing key anywhere in this repository. The two
    /// usual ways to keep "clone and run" working both make things worse: a key
    /// committed to <c>appsettings.json</c> has to be treated as compromised the
    /// moment it is pushed, and a placeholder is precisely the value that survives
    /// into production by accident.
    ///
    /// A key generated per process is strictly better than a shared development key:
    /// it is never written down, never shared between machines, and cannot be
    /// mistaken for something safe to deploy. The only cost is that restarting the
    /// application invalidates tokens it issued earlier, which is exactly what you
    /// want locally and unacceptable in production - hence the environment check.
    ///
    /// Outside Development nothing is generated, and <c>JwtOptions</c> validation
    /// stops startup with a message naming the variable to set.
    /// </summary>
    public static void UseEphemeralJwtSigningKeyInDevelopment(
        this IConfigurationManager configuration,
        IHostEnvironment environment)
    {
        const string SigningKeyPath = "Jwt:SigningKey";

        // Anything actually configured - environment variable, user-secrets, a
        // secret store - wins. This only fills a genuine gap.
        if (!string.IsNullOrWhiteSpace(configuration[SigningKeyPath]))
        {
            return;
        }

        if (!environment.IsDevelopment())
        {
            // Deliberately silent: JwtOptions.ValidateOnStart is what reports this,
            // and it already names the variable to set. Failing here would only
            // duplicate that message in a less helpful place.
            return;
        }

        // 48 random bytes, Base64-encoded to 64 characters - comfortably above the
        // 256-bit minimum that HS256 requires.
        var ephemeralKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { [SigningKeyPath] = ephemeralKey });

        Log.Warning(
            "No {SigningKeyPath} configured; generated a random development key. "
            + "Tokens issued now stop working when this process restarts. "
            + "Set the Jwt__SigningKey environment variable to pin it.",
            SigningKeyPath);
    }
}
