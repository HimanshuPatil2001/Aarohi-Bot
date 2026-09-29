using System.Text.Json.Serialization;

namespace AarohiBot.Api.Models.Groq;

public class GroqRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "llama-3.1-8b-instant";

    [JsonPropertyName("messages")]
    public List<GroqMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.75;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 250;
}
