using System.Data;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AarohiBot.Api.Repositories.Implementations;

public class UserFactRepository : IUserFactRepository
{
    private readonly string _connectionString;

    public UserFactRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<List<UserFact>> GetFactsByUserIdAsync(long userId)
    {
        const string sql = @"
            SELECT FactId, UserId, FactCategory, FactValue, ConfidenceScore, CreatedAt
            FROM dbo.UserFacts
            WHERE UserId = @UserId
            ORDER BY CreatedAt ASC;";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<UserFact>(sql, new { UserId = userId });
        return results.AsList();
    }

    public async Task<long> UpsertFactAsync(UserFact fact)
    {
        const string checkSql = @"
            SELECT FactId
            FROM dbo.UserFacts
            WHERE UserId = @UserId AND LOWER(FactCategory) = LOWER(@FactCategory);";

        const string updateSql = @"
            UPDATE dbo.UserFacts
            SET FactValue = @FactValue,
                ConfidenceScore = @ConfidenceScore,
                CreatedAt = SYSDATETIMEOFFSET()
            WHERE FactId = @FactId;";

        const string insertSql = @"
            INSERT INTO dbo.UserFacts (UserId, FactCategory, FactValue, ConfidenceScore, CreatedAt)
            OUTPUT INSERTED.FactId
            VALUES (@UserId, @FactCategory, @FactValue, @ConfidenceScore, @CreatedAt);";

        using var connection = CreateConnection();
        var existingFactId = await connection.QuerySingleOrDefaultAsync<long?>(checkSql, new { fact.UserId, fact.FactCategory });

        if (existingFactId.HasValue)
        {
            await connection.ExecuteAsync(updateSql, new
            {
                FactId = existingFactId.Value,
                fact.FactValue,
                fact.ConfidenceScore
            });
            return existingFactId.Value;
        }
        else
        {
            return await connection.ExecuteScalarAsync<long>(insertSql, fact);
        }
    }
}
