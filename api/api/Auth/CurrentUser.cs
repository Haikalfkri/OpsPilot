using System.Security.Claims;
using OpsPilot.Application.Common;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => ReadGuidClaim(AuthClaims.Sub);

    public Guid TeamId => ReadGuidClaim(AuthClaims.TeamId);

    public UserRole Role
    {
        get
        {
            var value = Principal?.FindFirst(AuthClaims.Role)?.Value;
            return Enum.TryParse<UserRole>(value, out var role)
                ? role
                : throw new AuthenticationFailedException();
        }
    }

    private Guid ReadGuidClaim(string claimType)
    {
        var value = Principal?.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var id)
            ? id
            : throw new AuthenticationFailedException();
    }
}