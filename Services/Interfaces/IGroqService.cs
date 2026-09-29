using AarohiBot.Api.Models.Groq;

namespace AarohiBot.Api.Services.Interfaces;

public interface IGroqService
{
    Task<string> GenerateResponseAsync(List<GroqMessage> messages, bool useDeepModel = false);
}
