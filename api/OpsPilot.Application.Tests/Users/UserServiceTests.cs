using FluentValidation;
using NSubstitute;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.Application.Tests.Users;

public class UserServiceTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Guid _myTeamId = Guid.NewGuid();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _currentUser.TeamId.Returns(_myTeamId);
        _currentUser.Role.Returns(UserRole.Admin);   // default: pemanggil adalah Admin

        _sut = new UserService(
            new ListUsersQueryValidator(),
            new CreateUserRequestValidator(),
            new UpdateUserRequestValidator(),
            _identity,
            _currentUser);
    }

    private UserInfo MakeUser(UserRole role = UserRole.Responder, bool isActive = true, Guid? teamId = null) =>
        new(Guid.NewGuid(), "Some User", "some@opspilot.local", role, teamId ?? _myTeamId, isActive);

    [Theory]
    [InlineData(UserRole.Responder)]
    [InlineData(UserRole.Viewer)]
    public async Task List_AsNonAdmin_ThrowsForbidden_AndNeverReadsUsers(UserRole role)
    {
        _currentUser.Role.Returns(role);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.ListAsync(new ListUsersQuery(), CancellationToken.None));

        await _identity.DidNotReceive().ListTeamUsersAsync(
            Arg.Any<Guid>(), Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_AsAdmin_AlwaysUsesTheTeamFromTheToken_AndMapsToDtos()
    {
        var user = MakeUser(UserRole.Viewer);
        _identity
            .ListTeamUsersAsync(_myTeamId, Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserInfo>(new[] { user }, 1, 20, 1));

        var result = await _sut.ListAsync(new ListUsersQuery(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(user.Email, result.Items[0].Email);
    }

    [Fact]
    public async Task List_WithInvalidQuery_ThrowsValidation_AndNeverReadsUsers()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ListAsync(new ListUsersQuery(PageSize: 1000), CancellationToken.None));

        await _identity.DidNotReceive().ListTeamUsersAsync(
            Arg.Any<Guid>(), Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>());
    }

    // ==================== GetById ====================

    [Fact]
    public async Task GetById_InMyTeam_ReturnsTheUser()
    {
        var user = MakeUser();
        _identity.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task GetById_UserInAnotherTeam_ThrowsNotFound()
    {
        var stranger = MakeUser(teamId: Guid.NewGuid());
        _identity.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetByIdAsync(stranger.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetById_AsNonAdmin_ThrowsForbidden()
    {
        _currentUser.Role.Returns(UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // ==================== Create ====================

    private static CreateUserRequest ValidCreate() =>
        new("new@opspilot.local", "New User", "Secret123!", UserRole.Responder);

    [Theory]
    [InlineData(UserRole.Responder)]
    [InlineData(UserRole.Viewer)]
    public async Task Create_AsNonAdmin_ThrowsForbidden_AndCreatesNothing(UserRole role)
    {
        _currentUser.Role.Returns(role);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.CreateAsync(ValidCreate(), CancellationToken.None));

        await _identity.DidNotReceiveWithAnyArgs().CreateUserAsync(
            default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task Create_WithInvalidRequest_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync(ValidCreate() with { Password = "lemah" }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithExistingEmail_ThrowsConflict_AndCreatesNothing()
    {
        _identity.EmailExistsAsync("new@opspilot.local", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(ValidCreate(), CancellationToken.None));

        await _identity.DidNotReceiveWithAnyArgs().CreateUserAsync(
            default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task Create_Valid_CreatesTheUserInTheCallersTeam()
    {
        var created = MakeUser();
        _identity.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _identity
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                             Arg.Any<UserRole>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.CreateAsync(ValidCreate(), CancellationToken.None);

        Assert.Equal(created.Id, result.Id);

        // Bukti utama: tim yang dipakai adalah tim dari token (_myTeamId)
        await _identity.Received(1).CreateUserAsync(
            "new@opspilot.local", "New User", "Secret123!", UserRole.Responder, _myTeamId,
            Arg.Any<CancellationToken>());
    }

    // ==================== Update ====================

    [Fact]
    public async Task Update_AsNonAdmin_ThrowsForbidden()
    {
        _currentUser.Role.Returns(UserRole.Responder);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateUserRequest(DisplayName: "X"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_WithEmptyRequest_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateUserRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_UserInAnotherTeam_ThrowsNotFound_AndChangesNothing()
    {
        var stranger = MakeUser(teamId: Guid.NewGuid());
        _identity.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateAsync(stranger.Id, new UpdateUserRequest(IsActive: false), CancellationToken.None));

        await _identity.DidNotReceiveWithAnyArgs().UpdateUserAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task Update_DeactivatingTheLastActiveAdmin_ThrowsConflict()
    {
        var lastAdmin = MakeUser(UserRole.Admin);
        _identity.GetByIdAsync(lastAdmin.Id, Arg.Any<CancellationToken>()).Returns(lastAdmin);
        _identity.CountActiveAdminsAsync(_myTeamId, Arg.Any<CancellationToken>()).Returns(1);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateAsync(lastAdmin.Id, new UpdateUserRequest(IsActive: false), CancellationToken.None));

        await _identity.DidNotReceiveWithAnyArgs().UpdateUserAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task Update_DemotingTheLastActiveAdmin_ThrowsConflict()
    {
        var lastAdmin = MakeUser(UserRole.Admin);
        _identity.GetByIdAsync(lastAdmin.Id, Arg.Any<CancellationToken>()).Returns(lastAdmin);
        _identity.CountActiveAdminsAsync(_myTeamId, Arg.Any<CancellationToken>()).Returns(1);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateAsync(lastAdmin.Id, new UpdateUserRequest(Role: UserRole.Viewer), CancellationToken.None));
    }

    [Fact]
    public async Task Update_DeactivatingAnAdmin_WhenAnotherAdminExists_Succeeds()
    {
        var admin = MakeUser(UserRole.Admin);
        var after = admin with { IsActive = false };
        _identity.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        _identity.CountActiveAdminsAsync(_myTeamId, Arg.Any<CancellationToken>()).Returns(2);
        _identity
            .UpdateUserAsync(admin.Id, null, null, false, Arg.Any<CancellationToken>())
            .Returns(after);

        var result = await _sut.UpdateAsync(admin.Id, new UpdateUserRequest(IsActive: false), CancellationToken.None);

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Update_DeactivatingAResponder_NeverChecksTheAdminCount()
    {
        var responder = MakeUser(UserRole.Responder);
        _identity.GetByIdAsync(responder.Id, Arg.Any<CancellationToken>()).Returns(responder);
        _identity
            .UpdateUserAsync(responder.Id, null, null, false, Arg.Any<CancellationToken>())
            .Returns(responder with { IsActive = false });

        await _sut.UpdateAsync(responder.Id, new UpdateUserRequest(IsActive: false), CancellationToken.None);

        await _identity.DidNotReceive().CountActiveAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_RenamingTheLastAdmin_IsAllowed()
    {
        var lastAdmin = MakeUser(UserRole.Admin);
        _identity.GetByIdAsync(lastAdmin.Id, Arg.Any<CancellationToken>()).Returns(lastAdmin);
        _identity
            .UpdateUserAsync(lastAdmin.Id, "Nama Baru", null, null, Arg.Any<CancellationToken>())
            .Returns(lastAdmin with { DisplayName = "Nama Baru" });

        var result = await _sut.UpdateAsync(
            lastAdmin.Id, new UpdateUserRequest(DisplayName: "Nama Baru"), CancellationToken.None);

        Assert.Equal("Nama Baru", result.DisplayName);
        await _identity.DidNotReceive().CountActiveAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}