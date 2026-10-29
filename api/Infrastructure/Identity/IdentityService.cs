using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Identity;

// TimeProvider (jam sistem) ditambahkan untuk mengisi CreatedAtUtc user baru.
public sealed class IdentityService(UserManager<ApplicationUser> users, TimeProvider clock) : IIdentityService
{
    public async Task<UserInfo?> ValidateCredentialsAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
            return null;

        if (await users.IsLockedOutAsync(user))
            return null;

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }

        await users.ResetAccessFailedCountAsync(user);
        return ToUserInfo(user);
    }

    public async Task<UserInfo?> GetByIdAsync(Guid userId, CancellationToken ct)
    {
        return await users.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserInfo(u.Id, u.DisplayName, u.Email!, u.Role, u.TeamId, u.IsActive))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<UserInfo>> ListTeamUsersAsync(
        Guid teamId, ListUsersQuery query, CancellationToken ct)
    {
        var q = users.Users.AsNoTracking().Where(u => u.TeamId == teamId);

        if (query.Role is { } role)
            q = q.Where(u => u.Role == role);

        if (query.IsActive is { } isActive)
            q = q.Where(u => u.IsActive == isActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(u => u.DisplayName.ToLower().Contains(term)
                           || u.Email!.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserInfo(u.Id, u.DisplayName, u.Email!, u.Role, u.TeamId, u.IsActive))
            .ToListAsync(ct);

        return new PagedResult<UserInfo>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        // FindByEmailAsync tidak peka huruf besar-kecil (Identity memakai email yang dinormalisasi)
        return await users.FindByEmailAsync(email) is not null;
    }

    public async Task<UserInfo> CreateUserAsync(
        string email, string displayName, string password, UserRole role, Guid teamId, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,          // UserName disamakan dengan email
            Email = email,
            EmailConfirmed = true,     // tidak ada alur konfirmasi email di MVP
            DisplayName = displayName,
            Role = role,
            TeamId = teamId,
            IsActive = true,
            CreatedAtUtc = clock.GetUtcNow()
        };

        // CreateAsync meng-hash password; password asli tidak pernah disimpan.
        var result = await users.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            // Dua request bersamaan dengan email sama bisa lolos pengecekan di service,
            // lalu ditolak di sini. Terjemahkan ke 409.
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                throw new ConflictException("A user with this email already exists.");

            // Sisanya (misalnya aturan password milik Identity) dijadikan error validasi 400.
            var failures = result.Errors.Select(e => new ValidationFailure("Password", e.Description));
            throw new ValidationException(failures);
        }

        return ToUserInfo(user);
    }

    public async Task<UserInfo> UpdateUserAsync(
        Guid userId, string? displayName, UserRole? role, bool? isActive, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User not found.");

        // Hanya field yang dikirim (tidak null) yang diubah
        if (displayName is not null) user.DisplayName = displayName;
        if (role is not null) user.Role = role.Value;
        if (isActive is not null) user.IsActive = isActive.Value;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(e => e.Code));
            throw new InvalidOperationException($"Failed to update user {userId}: {codes}");
        }

        return ToUserInfo(user);
    }

    public async Task<int> CountActiveAdminsAsync(Guid teamId, CancellationToken ct)
    {
        return await users.Users.CountAsync(
            u => u.TeamId == teamId && u.Role == UserRole.Admin && u.IsActive, ct);
    }

    private static UserInfo ToUserInfo(ApplicationUser u) =>
        new(u.Id, u.DisplayName, u.Email!, u.Role, u.TeamId, u.IsActive);
}