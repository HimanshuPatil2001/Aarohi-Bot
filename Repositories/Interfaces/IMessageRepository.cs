using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Repositories.Interfaces;

public interface IMessageRepository
{
    Task<long> SaveMessageAsync(Message message);
    Task<List<Message>> GetRecentChatHistoryAsync(long userId, int limit = 10);
    Task<int> GetTotalMessageCountAsync(long userId);
}
