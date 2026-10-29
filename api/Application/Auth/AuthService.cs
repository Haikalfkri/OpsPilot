using FluentValidation;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Auth
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
        Task<UserDto> GetCurrentUserAsync(CancellationToken ct);
    }
    
    public sealed class AuthService(
        IValidator<LoginRequest> validator,
        IIdentityService identity,
        ITokenService tokens,
        ICurrentUser currentUser) : IAuthService
    {
        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            await validator.ValidateAndThrowAsync(request, ct);

            var user = await identity.ValidateCredentialsAsync(request.Email.Trim(), request.Password, ct)
                ?? throw new AuthenticationFailedException();

            var token = tokens.Create(user);
            return new LoginResponse(token.Token, "Bearer", token.ExpiresAtUtc, UserDto.From(user));
        }

        public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct)
        {
            var user = await identity.GetByIdAsync(currentUser.UserId, ct)
                ?? throw new AuthenticationFailedException();

            return UserDto.From(user);
        }
    }
}
