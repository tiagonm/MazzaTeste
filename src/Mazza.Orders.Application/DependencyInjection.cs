using System.Reflection;
using FluentValidation;
using Mazza.Orders.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Mazza.Orders.Application;

/// <summary>
/// Composition root for the Application layer.
///
/// Each layer exposes one registration method and owns its own wiring, so the API's
/// <c>Program.cs</c> never has to know that this layer uses MediatR or
/// FluentValidation - it just calls <c>AddApplication()</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);

            // Behaviour order is the request's path through the pipeline, and it is
            // deliberate: logging is outermost so that a request rejected by
            // validation is still logged, with the time it took to reject it.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Scanned rather than listed one by one: a new validator becomes active as
        // soon as it is written, which is the only way the pipeline stays honest.
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
