using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AarohiBot.Api.Models.Groq;
using AarohiBot.Api.Services.Interfaces;

namespace AarohiBot.Api.Services.Implementations;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GroqService> _logger;

    public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        var apiKey = _config["Groq:ApiKey"] ?? string.Empty;
        _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<string> GenerateResponseAsync(List<GroqMessage> messages, bool useDeepModel = false)
    {
        var modelId = useDeepModel 
            ? _config["Groq:DeepModel"] ?? "llama-3.3-70b-versatile"
            : _config["Groq:FastModel"] ?? "llama-3.1-8b-instant";

        var requestPayload = new GroqRequest
        {
            Model = modelId,
            Messages = messages,
            Temperature = 0.75,
            MaxTokens = 250
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestPayload), 
            Encoding.UTF8, 
            "application/json");

        try
        {
            var response = await _httpClient.PostAsync("chat/completions", jsonContent);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var reply = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return reply ?? "Arey, thoda network glitch ho gaya shayad! Fir se bolo?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Groq inference");
            return "Arey yaar, mera network thoda slow chal raha hai. Ek baar dubara bolo na?";
        }
    }
}
