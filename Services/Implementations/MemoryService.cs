using System.Text.Json;
using System.Text.Json.Serialization;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Models.Groq;
using AarohiBot.Api.Repositories.Interfaces;
using AarohiBot.Api.Services.Interfaces;

namespace AarohiBot.Api.Services.Implementations;

public class MemoryService : IMemoryService
{
    private readonly IUserFactRepository _factRepository;
    private readonly IGroqService _groqService;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IUserFactRepository factRepository,
        IGroqService groqService,
        ILogger<MemoryService> logger)
    {
        _factRepository = factRepository;
        _groqService = groqService;
        _logger = logger;
    }

    private class ExtractedFactDto
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    public async Task ExtractAndSaveFactsAsync(long userId, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || userMessage.Length < 3)
        {
            return;
        }

        try
        {
            var extractionPrompt = new List<GroqMessage>
            {
                new()
                {
                    Role = "system",
                    Content = """
                    You are an information extraction assistant. Extract explicit personal facts about the user from their text.
                    Categories include: Name, City, Job, Company, College, Hobby, Stress, Relationship, Pet, Food, Favorite.
                    Respond ONLY with a JSON array of objects with keys "category" and "value". If no personal facts are mentioned, return an empty JSON array [].
                    Example input: "I live in Delhi and work as a software engineer at Infosys"
                    Example output: [{"category":"City","value":"Delhi"},{"category":"Job","value":"Software Engineer"},{"category":"Company","value":"Infosys"}]
                    """
                },
                new()
                {
                    Role = "user",
                    Content = userMessage
                }
            };

            var rawResult = await _groqService.GenerateResponseAsync(extractionPrompt, useDeepModel: false);
            if (string.IsNullOrWhiteSpace(rawResult) || !rawResult.Trim().StartsWith("["))
            {
                return;
            }

            var facts = JsonSerializer.Deserialize<List<ExtractedFactDto>>(rawResult.Trim());
            if (facts != null)
            {
                foreach (var fact in facts)
                {
                    if (!string.IsNullOrWhiteSpace(fact.Category) && !string.IsNullOrWhiteSpace(fact.Value))
                    {
                        await _factRepository.UpsertFactAsync(new UserFact
                        {
                            UserId = userId,
                            FactCategory = fact.Category.Trim(),
                            FactValue = fact.Value.Trim(),
                            ConfidenceScore = 1.0,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract facts for UserId {UserId} from message: {Message}", userId, userMessage);
        }
    }
}
