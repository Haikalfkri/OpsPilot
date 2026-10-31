using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<UserInfo?> ValidateCredentialsAsync(string email, string password, CancellationToken ct);

    Task<UserInfo?> GetByIdAsync(Guid userId, CancellationToken ct);

    Task<PagedResult<UserInfo>> ListTeamUsersAsync(Guid teamId, ListUsersQuery query, CancellationToken ct);

    // apakah sudah ada user dengan email ini?
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);

    // membuat user baru di tim tertentu. Password disimpan dalam bentuk hash
    Task<UserInfo> CreateUserAsync(
        string email, string displayName, string password, UserRole role, Guid teamId, CancellationToken ct);

    // mengubah user. Parameter null berarti "jangan diubah"
    Task<UserInfo> UpdateUserAsync(
        Guid userId, string? displayName, UserRole? role, bool? isActive, CancellationToken ct);

    // jumlah Admin yang masih aktif di sebuah tim
    Task<int> CountActiveAdminsAsync(Guid teamId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}