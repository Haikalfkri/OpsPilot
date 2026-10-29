using FluentValidation;
using NSubstitute;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.Application.Tests.Auth;

public class AuthServiceTests
{
    // Objek tiruan: tidak ada database, tidak ada JWT sungguhan
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly AuthService _sut;   // "sut" = System Under Test, yang sedang dites

    private static readonly UserInfo Admin = new(
        Guid.NewGuid(), "Demo Admin", "admin@opspilot.local", UserRole.Admin, Guid.NewGuid(), true);

    public AuthServiceTests()
    {
        // Validator-nya ASLI (murah dan tanpa dependensi), sisanya tiruan
        _sut = new AuthService(new LoginRequestValidator(), _identity, _tokens, _currentUser);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        // Arrange
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        _identity
            .ValidateCredentialsAsync("admin@opspilot.local", "Secret123!", Arg.Any<CancellationToken>())
            .Returns(Admin);
        _tokens.Create(Admin).Returns(new AccessTokenResult("token-palsu", expires));

        // Act
        var result = await _sut.LoginAsync(new LoginRequest("admin@opspilot.local", "Secret123!"), CancellationToken.None);

        // Assert
        Assert.Equal("token-palsu", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(expires, result.ExpiresAtUtc);
        Assert.Equal(Admin.Id, result.User.Id);
        Assert.Equal(UserRole.Admin, result.User.Role);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreRejected_ThrowsAuthenticationFailed_AndIssuesNoToken()
    {
        _identity
            .ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserInfo?>(null));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest("admin@opspilot.local", "salah"), CancellationToken.None));

        _tokens.DidNotReceive().Create(Arg.Any<UserInfo>());
    }

    [Fact]
    public async Task Login_WithInvalidRequest_ThrowsValidation_AndNeverChecksCredentials()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.LoginAsync(new LoginRequest("", ""), CancellationToken.None));

        await _identity.DidNotReceive().ValidateCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsTheUserFromTheToken()
    {
        _currentUser.UserId.Returns(Admin.Id);
        _identity.GetByIdAsync(Admin.Id, Arg.Any<CancellationToken>()).Returns(Admin);

        var result = await _sut.GetCurrentUserAsync(CancellationToken.None);

        Assert.Equal(Admin.Email, result.Email);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserNoLongerExists_ThrowsAuthenticationFailed()
    {
        _currentUser.UserId.Returns(Guid.NewGuid());
        _identity
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserInfo?>(null));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.GetCurrentUserAsync(CancellationToken.None));
    }
}