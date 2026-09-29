using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Repositories.Interfaces;

public interface ILogRepository
{
    Task<long> LogErrorAsync(ErrorLog errorLog);
    Task<long> LogActivityAsync(ActivityLog activityLog);
}
