using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Incidents;
using OpsPilot.Application.Teams;
using OpsPilot.Application.Users;
using OpsPilot.Domain.Sla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            services.AddSingleton(SlaPolicy.Default());

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITeamsService, TeamService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IIncidentService, IncidentService>();

            return services;
        }
    }
}
