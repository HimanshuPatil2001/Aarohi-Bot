using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AarohiBot.Api.Services.Implementations;

public class RelationshipEngineService : IRelationshipEngineService
{
    private readonly string _connectionString;

    public RelationshipEngineService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    public async Task<UserRelationshipState> GetOrCreateStateAsync(long userId)
    {
        using var connection = new SqlConnection(_connectionString);
        var state = await connection.QueryFirstOrDefaultAsync<UserRelationshipState>(
            "SELECT * FROM dbo.UserRelationshipState WHERE UserId = @UserId", new { UserId = userId });

        if (state == null)
        {
            state = new UserRelationshipState
            {
                UserId = userId,
                FamiliarityScore = 10,
                TrustScore = 10,
                IntimacyScore = 5,
                CurrentStage = "Stranger",
                TotalInteractions = 0,
                LastInteractionAt = DateTimeOffset.UtcNow
            };
            var sql = @"
                INSERT INTO dbo.UserRelationshipState (UserId, FamiliarityScore, TrustScore, IntimacyScore, CurrentStage, TotalInteractions, LastInteractionAt)
                VALUES (@UserId, @FamiliarityScore, @TrustScore, @IntimacyScore, @CurrentStage, @TotalInteractions, @LastInteractionAt);";
            await connection.ExecuteAsync(sql, state);
        }

        return state;
    }

    public async Task<(UserRelationshipState State, UserMilestone? NewMilestone)> ProcessMessageAndEvaluateStateAsync(long userId, string messageText, int factCount)
    {
        using var connection = new SqlConnection(_connectionString);
        var state = await GetOrCreateStateAsync(userId);

        // 1. Calculate Score Increments
        state.TotalInteractions += 1;
        
        // Increase Familiarity with message count and stored facts
        int familiarityDelta = 1 + (factCount > 0 ? 2 : 0);
        state.FamiliarityScore = Math.Min(100, state.FamiliarityScore + familiarityDelta);

        // Increase Trust steadily with interaction consistency
        int trustDelta = 1;
        state.TrustScore = Math.Min(100, state.TrustScore + trustDelta);

        // Intimacy increases if message shows depth (longer text or late-night IST hours)
        var currentHourIst = DateTime.UtcNow.AddHours(5.5).Hour;
        bool isLateNight = currentHourIst >= 22 || currentHourIst <= 2;
        int intimacyDelta = (messageText.Length > 60 ? 1 : 0) + (isLateNight ? 2 : 0);
        state.IntimacyScore = Math.Min(100, state.IntimacyScore + intimacyDelta);

        // 2. Evaluate Relationship Stage Transitions
        string previousStage = state.CurrentStage;
        state.CurrentStage = DetermineStage(state.FamiliarityScore, state.TrustScore, state.IntimacyScore);

        // Update DB State
        var updateSql = @"
            UPDATE dbo.UserRelationshipState 
            SET FamiliarityScore = @FamiliarityScore,
                TrustScore = @TrustScore,
                IntimacyScore = @IntimacyScore,
                CurrentStage = @CurrentStage,
                TotalInteractions = @TotalInteractions,
                LastInteractionAt = SYSDATETIMEOFFSET()
            WHERE UserId = @UserId;";
        await connection.ExecuteAsync(updateSql, state);

        // 3. Check for Milestone Unlocks
        UserMilestone? unlockedMilestone = null;

        // Stage Upgrade Milestone Check
        if (state.CurrentStage != previousStage)
        {
            unlockedMilestone = await TryUnlockMilestoneAsync(connection, userId, $"STAGE_{state.CurrentStage.ToUpper().Replace(" ", "_")}", 
                $"Unlocked {state.CurrentStage} Stage!", 
                $"You and Aarohi have naturally grown closer to become {state.CurrentStage}s.");
        }
        // Special Event Check: First Late Night Chat
        else if (isLateNight && state.TotalInteractions <= 15)
        {
            unlockedMilestone = await TryUnlockMilestoneAsync(connection, userId, "MILESTONE_LATE_NIGHT",
                "Late Night Confidante",
                "You and Aarohi shared your first late-night conversation.");
        }

        return (state, unlockedMilestone);
    }

    private string DetermineStage(int familiarity, int trust, int intimacy)
    {
        if (familiarity >= 80 && trust >= 75 && intimacy >= 70) return "Close Friend";
        if (familiarity >= 50 && trust >= 45 && intimacy >= 40) return "Friend";
        if (familiarity >= 25 && trust >= 20) return "Acquaintance";
        return "Stranger";
    }

    private async Task<UserMilestone?> TryUnlockMilestoneAsync(SqlConnection connection, long userId, string code, string title, string description)
    {
        var existing = await connection.QueryFirstOrDefaultAsync<UserMilestone>(
            "SELECT * FROM dbo.UserMilestones WHERE UserId = @UserId AND MilestoneCode = @Code",
            new { UserId = userId, Code = code });

        if (existing != null) return null; // Already unlocked

        var milestone = new UserMilestone
        {
            UserId = userId,
            MilestoneCode = code,
            Title = title,
            Description = description,
            UnlockedAt = DateTimeOffset.UtcNow,
            IsNotified = false
        };

        var insertSql = @"
            INSERT INTO dbo.UserMilestones (UserId, MilestoneCode, Title, Description, UnlockedAt, IsNotified)
            VALUES (@UserId, @MilestoneCode, @Title, @Description, @UnlockedAt, 0);";
        await connection.ExecuteAsync(insertSql, milestone);

        return milestone;
    }
}
