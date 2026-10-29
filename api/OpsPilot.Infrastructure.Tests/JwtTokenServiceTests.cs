using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Identity;
using Xunit;

namespace OpsPilot.Infrastructure.Tests;

public class JwtTokenServiceTests
{
    private const string SigningKey = "a7f3c9e2b8de3d8124c9e2a5b7d0f6381c2b8614f9e5c64105f0a9c70a7b3d6f";

    private static readonly UserInfo Responder = new(
        Guid.NewGuid(), "Demo Responder", "responder@opspilot.local", UserRole.Responder, Guid.NewGuid(), true);

    private static JwtTokenService CreateService() =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "OpsPilot",
            Audience = "OpsPilot.Client",
            SigningKey = SigningKey,
            ExpiryMinutes = 30
        }),
            TimeProvider.System);

    [Fact]
    public void Create_PutsUserIdentityRoleAndTeamIntoClaims()
    {
        var result = CreateService().Create(Responder);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

        Assert.Equal(Responder.Id.ToString(), jwt.GetClaim(AuthClaims.Sub).Value);
        Assert.Equal(Responder.Email, jwt.GetClaim(AuthClaims.Email).Value);
        Assert.Equal("Responder", jwt.GetClaim(AuthClaims.Role).Value);
        Assert.Equal(Responder.TeamId.ToString(), jwt.GetClaim(AuthClaims.TeamId).Value);
        Assert.Equal("OpsPilot", jwt.Issuer);
    }

    [Fact]
    public void Create_SetsExpiryFromConfiguration()
    {
        var result = CreateService().Create(Responder);

        Assert.InRange(
            result.ExpiresAtUtc,
            DateTimeOffset.UtcNow.AddMinutes(29),
            DateTimeOffset.UtcNow.AddMinutes(31));
    }

    [Fact]
    public async Task Token_ValidatedWithTheSameKey_IsAccepted()
    {
        var token = CreateService().Create(Responder).Token;

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ParametersWithKey(SigningKey));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Token_ValidatedWithAnotherKey_IsRejected()
    {
        var token = CreateService().Create(Responder).Token;

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token, ParametersWithKey("kunci-lain-yang-sama-sekali-berbeda-32-karakter"));

        Assert.False(result.IsValid);
    }

    private static TokenValidationParameters ParametersWithKey(string key) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
}