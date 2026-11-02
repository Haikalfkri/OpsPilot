using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Notifications;

namespace OpsPilot.Api.Controllers
{
    [Authorize(Policy = Policies.AnyTeamMember)]
    public sealed class NotificationsController(INotificationService notificationService) : ApiControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<NotificationDto>>> List(
            [FromQuery] ListNotificationsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await notificationService.ListAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken cancellationToken)
        {
            var result = await notificationService.GetUnreadCountAsync(cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{id:guid}/read")]
        [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NotificationDto>> MarkRead(Guid id, CancellationToken cancellationToken)
        {
            var result = await notificationService.MarkReadAsync(id, cancellationToken);
            return Ok(result);
        }

        [HttpPost("read-all")]
        [ProducesResponseType(typeof(MarkAllReadResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MarkAllReadResultDto>> MarkAllRead(CancellationToken cancellationToken)
        {
            var result = await notificationService.MarkAllReadAsync(cancellationToken);
            return Ok(result);
        }
    }
}
