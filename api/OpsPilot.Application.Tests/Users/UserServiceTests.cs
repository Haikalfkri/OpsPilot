using FluentValidation;
using NSubstitute;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Auth;
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
        _sut = new UserService(new ListUsersQueryValidator(), _identity, _currentUser);
    }

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
        _currentUser.Role.Returns(UserRole.Admin);

        var user = new UserInfo(Guid.NewGuid(), "Demo Viewer", "viewer@opspilot.local", UserRole.Viewer, _myTeamId, true);
        _identity
            .ListTeamUsersAsync(_myTeamId, Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserInfo>(new[] { user }, Page: 1, PageSize: 20, TotalCount: 1));

        var result = await _sut.ListAsync(new ListUsersQuery(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("viewer@opspilot.local", result.Items[0].Email);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_AsAdmin_WithInvalidQuery_ThrowsValidation_AndNeverReadsUsers()
    {
        _currentUser.Role.Returns(UserRole.Admin);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ListAsync(new ListUsersQuery(PageSize: 1000), CancellationToken.None));

        await _identity.DidNotReceive().ListTeamUsersAsync(
            Arg.Any<Guid>(), Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>());
    }
}