using System.Data;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AarohiBot.Api.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        const string sql = @"
            SELECT UserId, TelegramId, FirstName, LastName, Username, 
                   LanguagePreference, RelationshipTier, IsActive, OnboardingStep, 
                   CreatedAt, LastActiveAt
            FROM dbo.Users
            WHERE TelegramId = @TelegramId;";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { TelegramId = telegramId });
    }

    public async Task<User?> GetByIdAsync(long userId)
    {
        const string sql = @"
            SELECT UserId, TelegramId, FirstName, LastName, Username, 
                   LanguagePreference, RelationshipTier, IsActive, OnboardingStep, 
                   CreatedAt, LastActiveAt
            FROM dbo.Users
            WHERE UserId = @UserId;";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { UserId = userId });
    }

    public async Task<long> CreateUserAsync(User user)
    {
        const string sql = @"
            INSERT INTO dbo.Users (TelegramId, FirstName, LastName, Username, LanguagePreference, RelationshipTier, IsActive, OnboardingStep, CreatedAt, LastActiveAt)
            OUTPUT INSERTED.UserId
            VALUES (@TelegramId, @FirstName, @LastName, @Username, @LanguagePreference, @RelationshipTier, @IsActive, @OnboardingStep, @CreatedAt, @LastActiveAt);";

        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<long>(sql, user);
    }

    public async Task UpdateUserAsync(User user)
    {
        const string sql = @"
            UPDATE dbo.Users
            SET FirstName = @FirstName,
                LastName = @LastName,
                Username = @Username,
                LanguagePreference = @LanguagePreference,
                RelationshipTier = @RelationshipTier,
                IsActive = @IsActive,
                OnboardingStep = @OnboardingStep,
                LastActiveAt = SYSDATETIMEOFFSET()
            WHERE UserId = @UserId;";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, user);
    }

    public async Task UpdateLastActiveAsync(long userId)
    {
        const string sql = @"
            UPDATE dbo.Users
            SET LastActiveAt = SYSDATETIMEOFFSET()
            WHERE UserId = @UserId;";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task UpdateOnboardingStepAsync(long userId, int step)
    {
        const string sql = @"
            UPDATE dbo.Users
            SET OnboardingStep = @Step,
                LastActiveAt = SYSDATETIMEOFFSET()
            WHERE UserId = @UserId;";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, Step = step });
    }
}
