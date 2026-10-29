using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Users
{
    // data yang dikirim Admin saat membuat user baru (POST /users).
    // user baru selalu masuk ke tim Admin yang membuatnya.
    public sealed record CreateUserRequest(
        string Email,
        string DisplayName,
        string Password,
        UserRole? Role);


    // data untuk mengubah user (PATCH /users/{id}).
    // semua field opsional: null berarti "jangan diubah".
    public sealed record UpdateUserRequest(
        string? DisplayName = null,
        UserRole? Role = null,
        bool? IsActive = null);
}
