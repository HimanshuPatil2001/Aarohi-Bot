namespace AarohiBot.Api.Models.Database;

public class UserRelationshipState
{
    public long UserId { get; set; }
    public int FamiliarityScore { get; set; }
    public int TrustScore { get; set; }
    public int IntimacyScore { get; set; }
    public string CurrentStage { get; set; } = "Stranger";
    public int TotalInteractions { get; set; }
    public DateTimeOffset LastInteractionAt { get; set; }
}
