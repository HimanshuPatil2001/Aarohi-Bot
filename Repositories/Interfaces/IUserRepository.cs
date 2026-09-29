using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId);
    Task<User?> GetByIdAsync(long userId);
    Task<long> CreateUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task UpdateLastActiveAsync(long userId);
    Task UpdateOnboardingStepAsync(long userId, int step);
}
