namespace AarohiBot.Api.Models.Database;

public class User
{
    public long UserId { get; set; }
    public long TelegramId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string LanguagePreference { get; set; } = "Hinglish";
    public string RelationshipTier { get; set; } = "Acquaintance";
    public bool IsActive { get; set; } = true;
    public int OnboardingStep { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
}
