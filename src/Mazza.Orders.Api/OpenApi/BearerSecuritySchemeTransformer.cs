using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Mazza.Orders.Api.OpenApi;

/// <summary>
/// Declares the JWT bearer scheme in the generated OpenAPI document and attaches it to
/// the operations that actually require it.
///
/// Without this the document is technically valid but useless in practice: the API
/// explorer shows no way to supply a token, so every protected endpoint answers 401
/// and the reviewer has to reach for curl. Two transformers rather than one, because
/// the scheme is a document-level concern and the requirement is a per-operation one.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    internal const string SchemeName = JwtBearerDefaults.AuthenticationScheme;

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by POST /auth/login.",
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Marks every operation whose endpoint carries authorization metadata as requiring
/// the bearer scheme.
///
/// Derived from the endpoint metadata rather than from a hand-maintained list, so an
/// endpoint added to the authorized group is documented correctly without anyone
/// having to update this file.
/// </summary>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any();

        if (!requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        // The reference has to be given the host document: without it the reference
        // cannot resolve the scheme it points at, and serialises as an empty object -
        // a document that looks fine but tells the client nothing.
        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeName, context.Document)] = [],
            },
        ];

        return Task.CompletedTask;
    }
}
