namespace AarohiBot.Api.Models.Database;

public class ErrorLog
{
    public long ErrorLogId { get; set; }
    public long? UserId { get; set; }
    public long? TelegramId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? UserAction { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public string? InnerException { get; set; }
    public string? RequestData { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
