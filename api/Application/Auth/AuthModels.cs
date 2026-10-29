using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Auth
{
    public sealed record LoginRequest(string Email, string Password);
    public sealed record UserDto(Guid Id, string DisplayName, string Email, UserRole Role, Guid TeamId, bool IsActive);
    public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc, UserDto User);
    public sealed record UserInfo(Guid Id, string DisplayName, string Email, UserRole Role, Guid TeamId, bool IsActive);
}
