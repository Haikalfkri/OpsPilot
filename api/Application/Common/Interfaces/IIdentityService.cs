using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<UserInfo?> ValidateCredentialsAsync(string email, string password, CancellationToken ct);
        Task<UserInfo?> GetByIdAsync(Guid userId, CancellationToken ct);
        Task<PagedResult<UserInfo>> ListTeamUsersAsync(Guid teamId, ListUsersQuery query, CancellationToken ct);
    }
}
