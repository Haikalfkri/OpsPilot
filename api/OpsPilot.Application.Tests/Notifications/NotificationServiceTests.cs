using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Notifications;
using OpsPilot.Application.Tests.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.Application.Tests.Notifications;

public class NotificationServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly MutableTimeProvider _clock = new(T0);

    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _someoneElse = Guid.NewGuid();
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _currentUser.UserId.Returns(_me);
        _currentUser.Role.Returns(UserRole.Viewer);   // semua role boleh membaca notifikasinya sendiri

        _sut = new NotificationService(new ListNotificationsQueryValidator(), _db, _currentUser, _clock);
    }

    // ---------- Helper ----------

    private async Task<Notification> SeedAsync(
        Guid recipientId, string title = "Hello", bool isRead = false, int minutesAfterT0 = 0)
    {
        var notification = Notification.Create(
            recipientId, null, NotificationType.IncidentAssigned, title, "Message", T0.AddMinutes(minutesAfterT0));

        if (isRead)
            notification.MarkAsRead(T0.AddMinutes(minutesAfterT0));

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        return notification;
    }

    // ==================== List ====================

    [Fact]
    public async Task List_ReturnsOnlyMyNotifications_NewestFirst()
    {
        await SeedAsync(_me, "Oldest", minutesAfterT0: 1);
        await SeedAsync(_me, "Newest", minutesAfterT0: 3);
        await SeedAsync(_me, "Middle", minutesAfterT0: 2);
        await SeedAsync(_someoneElse, "Not mine", minutesAfterT0: 4);

        var result = await _sut.ListAsync(new ListNotificationsQuery(), CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(new[] { "Newest", "Middle", "Oldest" }, result.Items.Select(n => n.Title).ToArray());
    }

    [Fact]
    public async Task List_FiltersByReadState()
    {
        await SeedAsync(_me, "Unread", isRead: false);
        await SeedAsync(_me, "Read", isRead: true);

        var unread = await _sut.ListAsync(new ListNotificationsQuery(IsRead: false), CancellationToken.None);
        var read = await _sut.ListAsync(new ListNotificationsQuery(IsRead: true), CancellationToken.None);

        Assert.Equal("Unread", Assert.Single(unread.Items).Title);
        Assert.Equal("Read", Assert.Single(read.Items).Title);
    }

    [Fact]
    public async Task List_Paging_SplitsIntoPages()
    {
        for (var i = 1; i <= 5; i++)
            await SeedAsync(_me, $"N{i}", minutesAfterT0: i);

        var page = await _sut.ListAsync(new ListNotificationsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(new[] { "N3", "N2" }, page.Items.Select(n => n.Title).ToArray());   // terbaru dulu: N5 N4 | N3 N2 | N1
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPges);
    }

    [Fact]
    public async Task List_WithInvalidPaging_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ListAsync(new ListNotificationsQuery(PageSize: 1000), CancellationToken.None));
    }

    // ==================== Unread count ====================

    [Fact]
    public async Task UnreadCount_CountsOnlyMyUnreadNotifications()
    {
        await SeedAsync(_me, isRead: false);
        await SeedAsync(_me, isRead: false);
        await SeedAsync(_me, isRead: true);
        await SeedAsync(_someoneElse, isRead: false);

        var result = await _sut.GetUnreadCountAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    // ==================== MarkRead ====================

    [Fact]
    public async Task MarkRead_OnMyNotification_SetsTheFlagAndTheTime()
    {
        var notification = await SeedAsync(_me);
        _clock.Now = T0.AddMinutes(10);

        var result = await _sut.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.True(result.IsRead);
        Assert.Equal(T0.AddMinutes(10), result.ReadAtUtc);
        Assert.True((await _db.Notifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task MarkRead_Twice_KeepsTheFirstReadTime()
    {
        var notification = await SeedAsync(_me);

        _clock.Now = T0.AddMinutes(10);
        await _sut.MarkReadAsync(notification.Id, CancellationToken.None);

        _clock.Now = T0.AddMinutes(99);
        var second = await _sut.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.Equal(T0.AddMinutes(10), second.ReadAtUtc);
    }

    [Fact]
    public async Task MarkRead_OnSomeoneElsesNotification_ThrowsNotFound_AndLeavesItUnread()
    {
        var theirs = await SeedAsync(_someoneElse);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.MarkReadAsync(theirs.Id, CancellationToken.None));

        Assert.False((await _db.Notifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task MarkRead_OnAnUnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.MarkReadAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // ==================== MarkAllRead ====================

    [Fact]
    public async Task MarkAllRead_MarksOnlyMyUnreadNotifications()
    {
        await SeedAsync(_me, "Mine 1");
        await SeedAsync(_me, "Mine 2");
        await SeedAsync(_me, "Already read", isRead: true);
        await SeedAsync(_someoneElse, "Theirs");

        var result = await _sut.MarkAllReadAsync(CancellationToken.None);

        Assert.Equal(2, result.MarkedCount);
        Assert.All(await _db.Notifications.Where(n => n.RecipientId == _me).ToListAsync(), n => Assert.True(n.IsRead));
        Assert.False((await _db.Notifications.SingleAsync(n => n.RecipientId == _someoneElse)).IsRead);
    }
}