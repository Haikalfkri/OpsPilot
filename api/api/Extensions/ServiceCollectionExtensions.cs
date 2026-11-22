using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using OpsPilot.Api.Auth;
using OpsPilot.Api.Middleware;
using OpsPilot.Api.Services;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Enums;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using OpsPilot.Api.RateLimiting;

namespace OpsPilot.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "Frontend";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ---------- Dasar ----------
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services
            .AddControllers(options =>
            {
                // Validasi kita lakukan di FluentValidation, jadi format error-nya seragam
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options =>
            {
                // Enum dikirim sebagai teks ("Admin"), bukan angka
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // ---------- Error handling ----------
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // ---------- Authentication ----------
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        // ---------- Authorization ----------
        services.AddAuthorization(options =>
        {
            // Aman secara default: semua endpoint wajib login, kecuali ditandai [AllowAnonymous]
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(Policies.AdminOnly,
                policy => policy.RequireRole(nameof(UserRole.Admin)));

            options.AddPolicy(Policies.CanManageIncidents,
                policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Responder)));

            options.AddPolicy(Policies.AnyTeamMember,
                policy => policy.RequireRole(
                    nameof(UserRole.Admin), nameof(UserRole.Responder), nameof(UserRole.Viewer)));
        });

        // ---------- CORS ----------
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
            options.AddPolicy(CorsPolicyName, policy =>
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

        // ---------- Swagger ----------
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "OpsPilot API", Version = "v1" });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Tempel token hasil login (tanpa kata 'Bearer')."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // ---- Pembatasan laju (login) ----
        services.AddOptions<LoginRateLimitOptions>()
            .Bind(configuration.GetSection(LoginRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit >= 1 && o.WindowSeconds >= 1,
                "RateLimiting:Login: PermitLimit and WindowSeconds must be at least 1.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Batas dibaca saat permintaan datang (bukan saat aplikasi start), dari opsi di atas
            options.AddPolicy(RateLimitPolicies.Login, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
                return LoginRateLimit.GetPartition(context, limits);
            });

            // Jawaban 429 memakai bentuk Problem Details yang sama dengan error lain
            options.OnRejected = async (context, _) =>
            {
                var http = context.HttpContext;

                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : 60;

                http.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                await Results.Problem(
                        title: "Too many requests",
                        detail: "Too many sign-in attempts. Please wait a moment and try again.",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        type: "https://httpstatuses.com/429",
                        extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier })
                    .ExecuteAsync(http);
            };
        });

        return services;
    }
}