using System.Security.Claims;
using System.Text;
using Mazza.Orders.Application.Common.Abstractions.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mazza.Orders.Infrastructure.Identity;

/// <summary>
/// Issues HS256-signed JWTs.
///
/// Uses <see cref="JsonWebTokenHandler"/> rather than the older
/// <c>JwtSecurityTokenHandler</c>: it is the currently maintained implementation,
/// allocates less, and does not silently remap claim types the way the legacy handler
/// does (which is what turns <c>sub</c> into a long WS-Federation URI and surprises
/// everyone reading claims later).
/// </summary>
internal sealed class JwtAccessTokenFactory : IAccessTokenFactory
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenFactory(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Create(UserAccount user)
    {
        var issuedAt = _timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                // A unique token id, so an individual token can be revoked or traced
                // in logs without touching every other token for the same user.
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [ClaimTypes.Name] = user.Email,
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
