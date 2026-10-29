using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common;
using OpsPilot.Application.Common.Interfaces;

namespace OpsPilot.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessTokenResult Create(UserInfo user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(jwt.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(AuthClaims.Sub, user.Id.ToString()),
                new Claim(AuthClaims.Email, user.Email),
                new Claim(AuthClaims.Role, user.Role.ToString()),
                new Claim(AuthClaims.TeamId, user.TeamId.ToString()),
                new Claim("jti", Guid.NewGuid().ToString())
            }),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new AccessTokenResult(_handler.CreateToken(descriptor), expires);
    }
}