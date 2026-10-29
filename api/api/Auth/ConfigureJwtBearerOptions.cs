using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Application.Common;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Infrastructure.Identity;

namespace OpsPilot.Api.Auth;

public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        var jwt = jwtOptions.Value;

        // Jangan ubah nama claim; kita pakai nama pendek ("sub", "role", "team_id")
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = AuthClaims.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = EnsureUserStillValidAsync
        };
    }

    // Dijalankan di SETIAP request setelah token dinyatakan sah
    private static async Task EnsureUserStillValidAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;

        if (!Guid.TryParse(principal?.FindFirst(AuthClaims.Sub)?.Value, out var userId))
        {
            context.Fail("Subject token tidak valid.");
            return;
        }

        var identityService = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
        var user = await identityService.GetByIdAsync(userId, context.HttpContext.RequestAborted);

        if (user is null || !user.IsActive)
        {
            context.Fail("User tidak aktif atau sudah tidak ada.");
            return;
        }

        // Role atau tim berubah sejak token dibuat -> paksa login ulang
        var tokenRole = principal!.FindFirst(AuthClaims.Role)?.Value;
        var tokenTeam = principal.FindFirst(AuthClaims.TeamId)?.Value;

        if (tokenRole != user.Role.ToString() || tokenTeam != user.TeamId.ToString())
            context.Fail("Token sudah usang.");
    }
}