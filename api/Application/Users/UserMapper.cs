using OpsPilot.Application.Auth;
using OpsPilot.Application.Teams;

namespace OpsPilot.Application.Users;
public static class UserMapper
{
    public static UserDto ToDto(this UserInfo user) =>
        new(user.Id, user.DisplayName, user.Email, user.Role, user.TeamId, user.IsActive);

    public static TeamMemberDto ToMemberDto(this UserInfo user) =>
        new(user.Id, user.DisplayName, user.Role);
}