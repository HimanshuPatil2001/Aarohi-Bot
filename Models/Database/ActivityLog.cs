namespace AarohiBot.Api.Models.Database;

public class ActivityLog
{
    public long ActivityLogId { get; set; }
    public long? UserId { get; set; }
    public long? TelegramId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Metadata { get; set; }
    public long? DurationMs { get; set; }
    public bool IsSuccess { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
