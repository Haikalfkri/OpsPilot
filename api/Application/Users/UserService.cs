using FluentValidation;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Users;

public sealed record ListUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    UserRole? Role = null,
    bool? IsActive = null);

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("Search is too long.");
        RuleFor(x => x.Role).IsInEnum().When(x => x.Role.HasValue);
    }
}

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);
}


public sealed class UserService(
    IValidator<ListUsersQuery> listValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IIdentityService identity,
    ICurrentUser currentUser) : IUserService
{
    public async Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct)
    {
        EnsureAdmin();
        await listValidator.ValidateAndThrowAsync(query, ct);

        var page = await identity.ListTeamUsersAsync(currentUser.TeamId, query, ct);
        return page.Map(u => u.ToDto());
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        EnsureAdmin();

        var user = await FindUserInMyTeamAsync(id, ct);
        return user.ToDto();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        EnsureAdmin();
        await createValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim();

        if (await identity.EmailExistsAsync(email, ct))
            throw new ConflictException("A user with this email already exists.");

        var created = await identity.CreateUserAsync(
            email,
            request.DisplayName.Trim(),
            request.Password,
            request.Role!.Value,
            currentUser.TeamId,
            ct);

        return created.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        EnsureAdmin();
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var user = await FindUserInMyTeamAsync(id, ct);

        var losesAdminPower =
            user.Role == UserRole.Admin
            && user.IsActive
            && (request.IsActive == false
                || (request.Role is not null && request.Role != UserRole.Admin));

        if (losesAdminPower)
        {
            var activeAdmins = await identity.CountActiveAdminsAsync(currentUser.TeamId, ct);

            if (activeAdmins <= 1)
                throw new ConflictException("Cannot deactivate or demote the last active Admin of the team.");
        }

        var updated = await identity.UpdateUserAsync(
            id,
            request.DisplayName?.Trim(),
            request.Role,
            request.IsActive,
            ct);

        return updated.ToDto();
    }

    private void EnsureAdmin()
    {
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();
    }
    private async Task<UserInfo> FindUserInMyTeamAsync(Guid id, CancellationToken ct)
    {
        var user = await identity.GetByIdAsync(id, ct);

        if (user is null || user.TeamId != currentUser.TeamId)
            throw new NotFoundException("User not found.");

        return user;
    }
}