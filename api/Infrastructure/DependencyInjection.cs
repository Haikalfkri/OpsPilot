using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Infrastructure.Identity;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Persistence.Seed;

namespace OpsPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Jam sistem (bisa diganti jam palsu saat testing)
        services.TryAddSingleton(TimeProvider.System);

        // ---------- Database ----------
        var postgres = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(postgres))
            throw new InvalidOperationException("ConnectionStrings:Postgres belum diisi.");

        services.AddDbContext<OpsPilotDbContext>(options => options.UseNpgsql(postgres));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OpsPilotDbContext>());

        // ---------- Identity ----------
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<OpsPilotDbContext>();

        // ---------- JWT ----------
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Jwt:SigningKey harus diisi (minimal 32 karakter) lewat user-secrets.")
            .Validate(o => o.ExpiryMinutes > 0, "Jwt:ExpiryMinutes harus lebih dari 0.")
            .ValidateOnStart();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DevelopmentDataSeeder>();

        // ---------- Health check + Redis ----------
        var healthChecks = services.AddHealthChecks()
            .AddDbContextCheck<OpsPilotDbContext>("postgres");

        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = "opspilot:";
            });

            healthChecks.AddRedis(redis, name: "redis");
        }

        return services;
    }
}