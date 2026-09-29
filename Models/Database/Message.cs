namespace AarohiBot.Api.Models.Database;

public class Message
{
    public long MessageId { get; set; }
    public long UserId { get; set; }
    public string Sender { get; set; } = "User"; // "User" or "Bot"
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; } = 0;
    public string? ModelUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
