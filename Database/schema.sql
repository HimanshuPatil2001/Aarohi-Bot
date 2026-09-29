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

-- 2. Messages Table (Chat History)
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

-- 3. UserFacts Table (Relational Context Memory)
IF OBJECT_ID('dbo.UserFacts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserFacts (
        FactId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        FactCategory NVARCHAR(50) NOT NULL, -- e.g., 'Name', 'Job', 'City', 'Stress', 'Hobby'
        FactValue NVARCHAR(500) NOT NULL,
        ConfidenceScore FLOAT NOT NULL DEFAULT 1.0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_UserFacts_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserFacts_UserId ON dbo.UserFacts(UserId);
END;
GO

-- 4. UserRelationshipState Table (Multi-dimensional Relationship Metrics)
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

-- 5. UserMilestones Table (Unlocked Relationship Milestones)
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

-- 6. ScheduledNotifications Table (For Retention / n8n Jobs)
IF OBJECT_ID('dbo.ScheduledNotifications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledNotifications (
        NotificationId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL,
        NotificationType NVARCHAR(50) NOT NULL, -- 'GoodMorning', 'GoodNight', 'DailyCheckIn'
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

-- 7. ErrorLogs Table (Exception & Error Tracking)
IF OBJECT_ID('dbo.ErrorLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErrorLogs (
        ErrorLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NULL,
        TelegramId BIGINT NULL,
        Source NVARCHAR(100) NOT NULL, -- e.g. 'TelegramController', 'BotMessagingService', 'GroqService', 'MemoryService'
        UserAction NVARCHAR(255) NULL, -- What the user was doing (e.g. 'IncomingChatMessage', 'WebhookUpdate', 'ScheduledDispatch')
        ErrorMessage NVARCHAR(MAX) NOT NULL,
        ExceptionType NVARCHAR(200) NULL,
        StackTrace NVARCHAR(MAX) NULL,
        InnerException NVARCHAR(MAX) NULL,
        RequestData NVARCHAR(MAX) NULL, -- Incoming message, parameters, or payload
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_ErrorLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE SET NULL
    );

    CREATE NONCLUSTERED INDEX IX_ErrorLogs_UserId ON dbo.ErrorLogs(UserId);
    CREATE NONCLUSTERED INDEX IX_ErrorLogs_TelegramId ON dbo.ErrorLogs(TelegramId);
    CREATE NONCLUSTERED INDEX IX_ErrorLogs_CreatedAt ON dbo.ErrorLogs(CreatedAt DESC);
END;
GO

-- 8. ActivityLogs Table (User & System Action Audit Trail)
IF OBJECT_ID('dbo.ActivityLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ActivityLogs (
        ActivityLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NULL,
        TelegramId BIGINT NULL,
        ActionType NVARCHAR(100) NOT NULL, -- e.g. 'ReceivedMessage', 'GroqInference', 'SentMessage', 'FactExtracted', 'WebhookReceived'
        Description NVARCHAR(MAX) NULL,    -- Details of the action/interaction
        Metadata NVARCHAR(MAX) NULL,       -- Additional JSON / context parameters
        DurationMs BIGINT NULL,            -- Processing duration in milliseconds
        IsSuccess BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_ActivityLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE SET NULL
    );

    CREATE NONCLUSTERED INDEX IX_ActivityLogs_UserId ON dbo.ActivityLogs(UserId);
    CREATE NONCLUSTERED INDEX IX_ActivityLogs_TelegramId ON dbo.ActivityLogs(TelegramId);
    CREATE NONCLUSTERED INDEX IX_ActivityLogs_CreatedAt ON dbo.ActivityLogs(CreatedAt DESC);
END;
GO
