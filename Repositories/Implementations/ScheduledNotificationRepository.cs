using System.Data;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AarohiBot.Api.Repositories.Implementations;

public class ScheduledNotificationRepository : IScheduledNotificationRepository
{
    private readonly string _connectionString;

    public ScheduledNotificationRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<long> ScheduleNotificationAsync(ScheduledNotification notification)
    {
        const string sql = @"
            INSERT INTO dbo.ScheduledNotifications (UserId, NotificationType, MessageTemplate, ScheduledFor, IsProcessed, ProcessedAt, CreatedAt)
            OUTPUT INSERTED.NotificationId
            VALUES (@UserId, @NotificationType, @MessageTemplate, @ScheduledFor, @IsProcessed, @ProcessedAt, @CreatedAt);";

        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<long>(sql, notification);
    }

    public async Task<List<ScheduledNotification>> GetPendingNotificationsAsync()
    {
        const string sql = @"
            SELECT NotificationId, UserId, NotificationType, MessageTemplate, ScheduledFor, IsProcessed, ProcessedAt, CreatedAt
            FROM dbo.ScheduledNotifications
            WHERE IsProcessed = 0 AND ScheduledFor <= SYSDATETIMEOFFSET()
            ORDER BY ScheduledFor ASC;";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<ScheduledNotification>(sql);
        return results.AsList();
    }

    public async Task MarkAsProcessedAsync(long notificationId)
    {
        const string sql = @"
            UPDATE dbo.ScheduledNotifications
            SET IsProcessed = 1,
                ProcessedAt = SYSDATETIMEOFFSET()
            WHERE NotificationId = @NotificationId;";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { NotificationId = notificationId });
    }
}
