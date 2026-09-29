using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;

namespace AarohiBot.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IScheduledNotificationRepository _notificationRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IScheduledNotificationRepository notificationRepo,
        IUserRepository userRepo,
        ITelegramBotClient botClient,
        ILogger<NotificationsController> logger)
    {
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
        _botClient = botClient;
        _logger = logger;
    }

    [HttpPost("process-pending")]
    public async Task<IActionResult> ProcessPendingNotifications()
    {
        var pendingList = await _notificationRepo.GetPendingNotificationsAsync();
        var processedCount = 0;

        foreach (var notification in pendingList)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(notification.UserId);
                if (user != null && user.IsActive)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: user.TelegramId, 
                        text: notification.MessageTemplate);

                    await _notificationRepo.MarkAsProcessedAsync(notification.NotificationId);
                    processedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch scheduled notification {NotificationId} to user {UserId}", 
                    notification.NotificationId, notification.UserId);
            }
        }

        return Ok(new { TotalPending = pendingList.Count, Processed = processedCount });
    }

    [HttpPost("schedule")]
    public async Task<IActionResult> ScheduleNotification([FromBody] ScheduledNotification notification)
    {
        if (notification.UserId <= 0 || string.IsNullOrWhiteSpace(notification.MessageTemplate))
        {
            return BadRequest("UserId and MessageTemplate are required.");
        }

        notification.CreatedAt = DateTimeOffset.UtcNow;
        if (notification.ScheduledFor == default)
        {
            notification.ScheduledFor = DateTimeOffset.UtcNow;
        }

        var id = await _notificationRepo.ScheduleNotificationAsync(notification);
        return Ok(new { NotificationId = id, Status = "Scheduled" });
    }
}
