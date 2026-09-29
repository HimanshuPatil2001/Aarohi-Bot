using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Models.Groq;

namespace AarohiBot.Api.Services.Interfaces;

public interface IPromptBuilderService
{
    List<GroqMessage> BuildPrompt(User user, UserRelationshipState relState, List<UserFact> facts, List<Message> chatHistory, string currentInput);
}
