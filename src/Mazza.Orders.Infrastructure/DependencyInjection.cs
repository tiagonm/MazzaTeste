using System.Text;
using Mazza.Orders.Application.Common.Abstractions.Identity;
using Mazza.Orders.Application.Common.Abstractions.Persistence;
using Mazza.Orders.Infrastructure.Identity;
using Mazza.Orders.Infrastructure.Persistence;
using Mazza.Orders.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mazza.Orders.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer: this is where the ports declared by
/// Application get bound to concrete technology (SQLite, JWT, PBKDF2).
///
/// Everything technology-specific is confined to this file and the classes it
/// registers, which is what makes the claim "Application does not depend on EF Core"
/// verifiable rather than aspirational - the Application .csproj simply has no EF
/// package reference.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddIdentityServices(configuration);

        // The BCL clock, registered as a dependency so handlers can be given a fake
        // one in tests instead of reading DateTime.UtcNow directly.
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDatabase")
            ?? "Data Source=orders.db";

        // SQLite has no native decimal type - it stores money as TEXT. Historically
        // that meant EF warned about unreliable ordering and comparison of decimal
        // columns; the EF Core 10 provider implements decimal comparison and
        // arithmetic itself, so no warning suppression is needed here. Money is still
        // mapped with explicit precision (18,2) in OrderItemConfiguration so values
        // round-trip exactly.
        services.AddDbContext<OrdersDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();

        // Same scoped DbContext instance behind both abstractions, so a repository
        // write and the commit that follows it are part of one transaction.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrdersDbContext>());

        return services;
    }

    private static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup, not at the first login attempt: a missing signing key
            // is a deployment error and should stop the container, not produce a 500
            // for the first user who tries to sign in.
            .ValidateOnStart();

        services.AddOptions<DevUserOptions>()
            .Bind(configuration.GetSection(DevUserOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IUserAuthenticator, InMemoryUserAuthenticator>();
        services.AddSingleton<IAccessTokenFactory, JwtAccessTokenFactory>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured from IOptions<JwtOptions> rather than from a second, independent
        // read of IConfiguration. Two reasons, and both matter:
        //
        // 1. The signing key used to validate a token is then guaranteed to be the
        //    same one JwtAccessTokenFactory signed it with - there is one source of
        //    truth instead of two reads that can disagree.
        // 2. It defers touching the key until after the options have been validated.
        //    Reading it eagerly here would hand an empty string to
        //    SymmetricSecurityKey and fail with a cryptography error, burying the
        //    "Jwt:SigningKey is not configured" message that actually tells the
        //    operator what to fix.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Tokens only ever arrive over the wire, so nothing here needs to be
                // lenient. Every one of these validations is on deliberately.
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    // Keep "sub" as "sub" instead of expanding it into a
                    // WS-Federation URI, matching how the token was issued.
                    NameClaimType = JwtRegisteredClaimNames.Email,
                };

                // No effect on the signature check: this only gates metadata
                // retrieval, and there is no Authority to retrieve metadata from.
                // A real deployment terminates TLS in front of this service.
                bearerOptions.RequireHttpsMetadata = false;
            });

        services.AddAuthorization();

        return services;
    }
}
