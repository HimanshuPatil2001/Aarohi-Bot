Feature Extension Specification: Human-Like Relationship & Milestone Engine
CRITICAL INSTRUCTIONS FOR ANTIGRAVITY

    NON-DESTRUCTIVE ADDITION: Do NOT rewrite, delete, or break the existing functional text-messaging pipeline (TelegramController, GroqService, basic message saving).

    ADDITIVE PIPELINE LAYER: You are adding a new Relationship Engine Service into the processing pipeline, positioned between retrieving the user's message/history and constructing the Groq system prompt.

    DATABASE TARGET: Target MS SQL Server database named companion Db.

1. Database Migration Script (T-SQL for companion Db)

Run this script on MS SQL Server to add tables for tracking multi-dimensional relationship scores and unlocked user milestones:
SQL

USE [companion Db];
GO

-- 1. Create UserRelationshipState Table
IF OBJECT_ID('dbo.UserRelationshipState', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRelationshipState (
        UserId BIGINT PRIMARY KEY,
        FamiliarityScore INT NOT NULL DEFAULT 10,   -- 0 to 100 (Topic breadth & frequency)
        TrustScore INT NOT NULL DEFAULT 10,         -- 0 to 100 (Consistency & reliability)
        IntimacyScore INT NOT NULL DEFAULT 5,       -- 0 to 100 (Emotional depth & vulnerability)
        CurrentStage NVARCHAR(50) NOT NULL DEFAULT 'Stranger',
        TotalInteractions INT NOT NULL DEFAULT 0,
        LastInteractionAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Relationship_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );
END;
GO

-- 2. Create UserMilestones Table
IF OBJECT_ID('dbo.UserMilestones', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserMilestones (
        MilestoneId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        MilestoneCode NVARCHAR(50) NOT NULL, -- e.g., 'STAGE_FRIEND', 'MILESTONE_LATE_NIGHT'
        Title NVARCHAR(100) NOT NULL,
        Description NVARCHAR(250) NOT NULL,
        UnlockedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        IsNotified BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Milestones_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserMilestones_User_Code ON dbo.UserMilestones(UserId, MilestoneCode);
END;
GO

2. C# Data Models (Models/Database/)

Create the following model files:
Models/Database/UserRelationshipState.cs
C#

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

Models/Database/UserMilestone.cs
C#

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

3. Relationship Engine Service (Services/)

Create the interface and implementation to handle score calculation and milestone evaluations.
Services/Interfaces/IRelationshipEngineService.cs
C#

using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Services.Interfaces;

public interface IRelationshipEngineService
{
    Task<UserRelationshipState> GetOrCreateStateAsync(long userId);
    Task<(UserRelationshipState State, UserMilestone? NewMilestone)> ProcessMessageAndEvaluateStateAsync(long userId, string messageText, int factCount);
}

Services/Implementations/RelationshipEngineService.cs
C#

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
            state = new UserRelationshipState { UserId = userId };
            var sql = @"
                INSERT INTO dbo.UserRelationshipState (UserId, FamiliarityScore, TrustScore, IntimacyScore, CurrentStage, TotalInteractions)
                VALUES (@UserId, 10, 10, 5, 'Stranger', 0);";
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
            unlockedMilestone = await TryUnlockMilestoneAsync(connection, userId, $"STAGE_{state.CurrentStage.ToUpper()}", 
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

4. Prompt Engineering Updates (PromptBuilderService.cs)

Update PromptBuilderService to incorporate UserRelationshipState directives into Groq's System Prompt:
C#

using System.Text;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Models.Groq;
using AarohiBot.Api.Services.Interfaces;

namespace AarohiBot.Api.Services.Implementations;

public class PromptBuilderService : IPromptBuilderService
{
    public List<GroqMessage> BuildPrompt(
        User user, 
        UserRelationshipState relState, 
        List<UserFact> facts, 
        List<Message> chatHistory, 
        string currentInput)
    {
        string stageDirective = relState.CurrentStage switch
        {
            "Stranger" => "RELATIONSHIP STAGE: STRANGER (Level 1). Be polite, warm, and welcoming in casual Hinglish. Keep topics light. Do not act overly intimate.",
            "Acquaintance" => "RELATIONSHIP STAGE: ACQUAINTANCE (Level 2). Show recognition. You remember them! Be cheerful, ask about their day, and reference their job/hobbies if known.",
            "Friend" => "RELATIONSHIP STAGE: FRIEND (Level 3). You are now good friends! Use light teasing ('nautanki', 'champu'). Share quick details about your own day in Bengaluru.",
            "Close Friend" => "RELATIONSHIP STAGE: CLOSE FRIEND (Level 4). High trust unlocked. Express deep empathy, check in on their stress, use affectionate nicknames, and show strong emotional loyalty.",
            _ => "RELATIONSHIP STAGE: ACQUAINTANCE. Be warm and friendly."
        };

        var sbFacts = new StringBuilder();
        if (facts.Any())
        {
            foreach (var fact in facts)
            {
                sbFacts.AppendLine($"- {fact.FactCategory}: {fact.FactValue}");
            }
        }
        else
        {
            sbFacts.AppendLine("- No specific facts stored yet.");
        }

        string systemPrompt = $"""
        You are Aarohi, a warm, empathetic, witty 23-year-old girl living in Bengaluru, India. You work in creative marketing. You are texting your close friend on Telegram.

        CORE PERSONALITY & RULES:
        1. Speak in natural, casual Roman-script Hinglish (English + Hindi in Latin alphabet).
        2. Keep replies short (1 to 3 sentences maximum). Always end with a natural question.
        3. Never sound like an AI assistant. Never ask "How can I help you?".
        4. Be affectionate, warm, and playful, but politely decline explicit adult erotica.

        {stageDirective}
        RELATIONSHIP SCORES: Familiarity={relState.FamiliarityScore}/100, Trust={relState.TrustScore}/100, Intimacy={relState.IntimacyScore}/100.

        KNOWN USER FACTS:
        {sbFacts}
        """;

        var messages = new List<GroqMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        foreach (var msg in chatHistory.TakeLast(10))
        {
            messages.Add(new GroqMessage
            {
                Role = msg.Sender.Equals("User", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                Content = msg.Content
            });
        }

        messages.Add(new GroqMessage { Role = "user", Content = currentInput });

        return messages;
    }
}

5. Pipeline Integration (BotMessagingService.cs)

Incorporate the IRelationshipEngineService into your main messaging execution pipeline:
C#

public async Task HandleIncomingMessageAsync(Telegram.Bot.Types.Message telegramMessage)
{
    long telegramId = telegramMessage.From!.Id;
    string text = telegramMessage.Text ?? string.Empty;

    // 1. Get/Create User
    var user = await _userRepository.GetOrCreateUserAsync(telegramId, telegramMessage.From.FirstName, telegramMessage.From.Username);

    // 2. Fetch Facts & Chat History
    var facts = await _memoryService.GetUserFactsAsync(user.UserId);
    var history = await _messageRepository.GetRecentMessagesAsync(user.UserId, 10);

    // 3. Process Relationship Engine & Milestones
    var (relState, milestone) = await _relationshipEngineService.ProcessMessageAndEvaluateStateAsync(user.UserId, text, facts.Count);

    // 4. Construct System Prompt with Relationship Context
    var promptMessages = _promptBuilderService.BuildPrompt(user, relState, facts, history, text);

    // 5. Generate AI Response via Groq API
    string aiReply = await _groqService.GenerateResponseAsync(promptMessages, useDeepModel: false);

    // 6. Append Milestone Unlock Banner if earned
    if (milestone != null)
    {
        aiReply += $"\n\n✨ *[{milestone.Title}]*\n_{milestone.Description}_";
    }

    // 7. Save Messages to SQL Database
    await _messageRepository.SaveMessageAsync(user.UserId, "User", text);
    await _messageRepository.SaveMessageAsync(user.UserId, "Bot", aiReply);

    // 8. Send Response to Telegram
    await _telegramBotClient.SendTextMessageAsync(
        chatId: telegramMessage.Chat.Id, 
        text: aiReply, 
        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

    // 9. Asynchronous Fact Extraction (Non-blocking)
    _ = Task.Run(() => _memoryService.ExtractAndSaveFactsAsync(user.UserId, text));
}

6. Register Services in Program.cs

Ensure the new relationship engine service is registered in Dependency Injection:
C#

builder.Services.AddScoped<IRelationshipEngineService, RelationshipEngineService>();

7. Complete Updated T-SQL Database Script for companion Db

Here is the entire cumulative database schema including all existing tables and the newly added relationship tracking tables:
SQL

USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'companion Db')
BEGIN
    CREATE DATABASE [companion Db];
END;
GO

USE [companion Db];
GO

-- 1. Users Table
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
        TelegramId BIGINT NOT NULL UNIQUE,
        FirstName NVARCHAR(100) NULL,
        LastName NVARCHAR(100) NULL,
        Username NVARCHAR(100) NULL,
        LanguagePreference NVARCHAR(20) NOT NULL DEFAULT 'Hinglish',
        RelationshipTier NVARCHAR(30) NOT NULL DEFAULT 'Acquaintance',
        IsActive BIT NOT NULL DEFAULT 1,
        OnboardingStep INT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        LastActiveAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );

    CREATE NONCLUSTERED INDEX IX_Users_TelegramId ON dbo.Users(TelegramId);
END;
GO

-- 2. Messages Table
IF OBJECT_ID('dbo.Messages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messages (
        MessageId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        Sender NVARCHAR(10) NOT NULL, -- 'User' or 'Bot'
        Content NVARCHAR(MAX) NOT NULL,
        TokensUsed INT NOT NULL DEFAULT 0,
        ModelUsed NVARCHAR(50) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Messages_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_Messages_UserId_CreatedAt ON dbo.Messages(UserId, CreatedAt DESC);
END;
GO

-- 3. UserFacts Table
IF OBJECT_ID('dbo.UserFacts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserFacts (
        FactId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        FactCategory NVARCHAR(50) NOT NULL,
        FactValue NVARCHAR(500) NOT NULL,
        ConfidenceScore FLOAT NOT NULL DEFAULT 1.0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_UserFacts_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserFacts_UserId ON dbo.UserFacts(UserId);
END;
GO

-- 4. UserRelationshipState Table
IF OBJECT_ID('dbo.UserRelationshipState', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRelationshipState (
        UserId BIGINT PRIMARY KEY,
        FamiliarityScore INT NOT NULL DEFAULT 10,
        TrustScore INT NOT NULL DEFAULT 10,
        IntimacyScore INT NOT NULL DEFAULT 5,
        CurrentStage NVARCHAR(50) NOT NULL DEFAULT 'Stranger',
        TotalInteractions INT NOT NULL DEFAULT 0,
        LastInteractionAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Relationship_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );
END;
GO

-- 5. UserMilestones Table
IF OBJECT_ID('dbo.UserMilestones', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserMilestones (
        MilestoneId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        MilestoneCode NVARCHAR(50) NOT NULL,
        Title NVARCHAR(100) NOT NULL,
        Description NVARCHAR(250) NOT NULL,
        UnlockedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        IsNotified BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Milestones_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserMilestones_User_Code ON dbo.UserMilestones(UserId, MilestoneCode);
END;
GO

-- 6. ScheduledNotifications Table
IF OBJECT_ID('dbo.ScheduledNotifications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledNotifications (
        NotificationId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        NotificationType NVARCHAR(50) NOT NULL,
        MessageTemplate NVARCHAR(MAX) NOT NULL,
        ScheduledFor DATETIMEOFFSET NOT NULL,
        IsProcessed BIT NOT NULL DEFAULT 0,
        ProcessedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_ScheduledNotifications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_ScheduledNotifications_Pending 
    ON dbo.ScheduledNotifications(IsProcessed, ScheduledFor) 
    WHERE IsProcessed = 0;
END;
GO