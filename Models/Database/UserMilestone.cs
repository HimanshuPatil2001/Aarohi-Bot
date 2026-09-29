namespace AarohiBot.Api.Models.Database;

public class UserMilestone
{
    public long MilestoneId { get; set; }
    public long UserId { get; set; }
    public string MilestoneCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset UnlockedAt { get; set; }
    public bool IsNotified { get; set; }
}
