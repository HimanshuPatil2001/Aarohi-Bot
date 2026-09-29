namespace AarohiBot.Api.Models.Database;

public class ScheduledNotification
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty; // 'GoodMorning', 'GoodNight', 'DailyCheckIn'
    public string MessageTemplate { get; set; } = string.Empty;
    public DateTimeOffset ScheduledFor { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
