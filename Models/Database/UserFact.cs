namespace AarohiBot.Api.Models.Database;

public class UserFact
{
    public long FactId { get; set; }
    public long UserId { get; set; }
    public string FactCategory { get; set; } = string.Empty; // e.g. 'Name', 'Job', 'City', 'Stress', 'Hobby'
    public string FactValue { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } = 1.0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
