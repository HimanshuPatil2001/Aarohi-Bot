using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Repositories.Interfaces;

public interface IScheduledNotificationRepository
{
    Task<long> ScheduleNotificationAsync(ScheduledNotification notification);
    Task<List<ScheduledNotification>> GetPendingNotificationsAsync();
    Task MarkAsProcessedAsync(long notificationId);
}
