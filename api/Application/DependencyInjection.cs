using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Application.Activities;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Comments;
using OpsPilot.Application.Incidents;
using OpsPilot.Application.Sla;
using OpsPilot.Application.Teams;
using OpsPilot.Application.Users;

namespace OpsPilot.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITeamsService, TeamService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IIncidentService, IncidentService>();
            services.AddScoped<ICommentService, CommentService>();
            services.AddScoped<IActivityService, ActivityService>();
            services.AddScoped<ISlaService, SlaService>();

            return services;
        }
    }
}
