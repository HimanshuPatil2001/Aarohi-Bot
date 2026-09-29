using System.Data;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AarohiBot.Api.Repositories.Implementations;

public class MessageRepository : IMessageRepository
{
    private readonly string _connectionString;

    public MessageRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<long> SaveMessageAsync(Message message)
    {
        const string sql = @"
            INSERT INTO dbo.Messages (UserId, Sender, Content, TokensUsed, ModelUsed, CreatedAt)
            OUTPUT INSERTED.MessageId
            VALUES (@UserId, @Sender, @Content, @TokensUsed, @ModelUsed, @CreatedAt);";

        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<long>(sql, message);
    }

    public async Task<List<Message>> GetRecentChatHistoryAsync(long userId, int limit = 10)
    {
        const string sql = @"
            SELECT MessageId, UserId, Sender, Content, TokensUsed, ModelUsed, CreatedAt
            FROM (
                SELECT TOP (@Limit) MessageId, UserId, Sender, Content, TokensUsed, ModelUsed, CreatedAt
                FROM dbo.Messages
                WHERE UserId = @UserId
                ORDER BY CreatedAt DESC
            ) AS Recent
            ORDER BY CreatedAt ASC;";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<Message>(sql, new { UserId = userId, Limit = limit });
        return results.AsList();
    }

    public async Task<int> GetTotalMessageCountAsync(long userId)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM dbo.Messages
            WHERE UserId = @UserId;";

        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
    }
}
