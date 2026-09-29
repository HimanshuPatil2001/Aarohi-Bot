using AarohiBot.Api.Models.Database;

namespace AarohiBot.Api.Services.Interfaces;

public interface IRelationshipEngineService
{
    Task<UserRelationshipState> GetOrCreateStateAsync(long userId);
    Task<(UserRelationshipState State, UserMilestone? NewMilestone)> ProcessMessageAndEvaluateStateAsync(long userId, string messageText, int factCount);
}
