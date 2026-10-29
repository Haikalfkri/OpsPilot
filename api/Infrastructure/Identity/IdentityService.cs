using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Infrastructure.Identity
{
    public sealed class IdentityService(UserManager<ApplicationUser> users) : IIdentityService
    {
        public async Task<UserInfo?> ValidateCredentialsAsync(string email, string password, CancellationToken ct)
        {
            var user = await users.FindByEmailAsync(email);

            // user tidak ada, atau akun dinonaktifkan -> gagal (tanpa memberi tahu alasannya)
            if (user is null || !user.IsActive)
            {
                return null;
            }

            // akun terkunci karena terlalu banyak salah password -> gagal
            if (await users.IsLockedOutAsync(user))
            {
                return null;
            }

            if (!await users.CheckPasswordAsync(user, password))
            {
                await users.AccessFailedAsync(user); // // tambah hitungan gagal login
                return null;
            }

            await users.ResetAccessFailedCountAsync(user); // berhasil -> hitungan kembali 0
            return ToUserInfo(user);
        }

        public async Task<UserInfo?> GetByIdAsync(Guid userId, CancellationToken ct)
        {
            return await users.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserInfo(u.Id, u.DisplayName, u.Email!, u.Role, u.TeamId, u.IsActive))
                .FirstOrDefaultAsync();
        }

        public async Task<PagedResult<UserInfo>> ListTeamUsersAsync(
            Guid teamId, ListUsersQuery query, CancellationToken ct)
        {
            // Selalu mulai dari user di tim yang diminta
            var q = users.Users.AsNoTracking().Where(u => u.TeamId == teamId);

            // Filter opsional
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

        private static UserInfo ToUserInfo(ApplicationUser u) =>
            new(u.Id, u.DisplayName, u.Email!, u.Role, u.TeamId, u.IsActive);
    }
}
