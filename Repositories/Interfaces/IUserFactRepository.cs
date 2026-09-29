using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Repositories.Interfaces;

public interface IUserFactRepository
{
    Task<List<UserFact>> GetFactsByUserIdAsync(long userId);
    Task<long> UpsertFactAsync(UserFact fact);
}
