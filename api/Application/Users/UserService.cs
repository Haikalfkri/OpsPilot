using FluentValidation;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Users
{
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
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");
            RuleFor(x => x.Search).MaximumLength(100).WithMessage("Search is too lgon.");
            RuleFor(x => x.Role).IsInEnum().When(x => x.Role.HasValue);
        }
    }

    public interface IUserService
    {
        Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct);
    }

    public sealed class UserService(
        IValidator<ListUsersQuery> validator,
        IIdentityService identity,
        ICurrentUser currentUser) : IUserService
    {
        public async Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct)
        {
            if (currentUser.Role != UserRole.Admin)
                throw new ForbiddenException();

            await validator.ValidateAndThrowAsync(query, ct);

            var page = await identity.ListTeamUsersAsync(currentUser.TeamId, query, ct);
            return page.Map(UserDto.From);
        }
    }
}
