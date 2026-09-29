using System.Data;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AarohiBot.Api.Repositories.Implementations;

public class LogRepository : ILogRepository
{
    private readonly string _connectionString;
    private readonly ILogger<LogRepository> _logger;

    public LogRepository(IConfiguration configuration, ILogger<LogRepository> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<long> LogErrorAsync(ErrorLog errorLog)
    {
        const string sql = @"
            INSERT INTO dbo.ErrorLogs (UserId, TelegramId, Source, UserAction, ErrorMessage, ExceptionType, StackTrace, InnerException, RequestData, CreatedAt)
            OUTPUT INSERTED.ErrorLogId
            VALUES (@UserId, @TelegramId, @Source, @UserAction, @ErrorMessage, @ExceptionType, @StackTrace, @InnerException, @RequestData, @CreatedAt);";

        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql, errorLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist error log to database. Original Error: {ErrorMessage}", errorLog.ErrorMessage);
            return 0;
        }
    }

    public async Task<long> LogActivityAsync(ActivityLog activityLog)
    {
        const string sql = @"
            INSERT INTO dbo.ActivityLogs (UserId, TelegramId, ActionType, Description, Metadata, DurationMs, IsSuccess, CreatedAt)
            OUTPUT INSERTED.ActivityLogId
            VALUES (@UserId, @TelegramId, @ActionType, @Description, @Metadata, @DurationMs, @IsSuccess, @CreatedAt);";

        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql, activityLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist activity log to database. Action: {ActionType}", activityLog.ActionType);
            return 0;
        }
    }
}
