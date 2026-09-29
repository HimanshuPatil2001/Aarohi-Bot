using System.Text.Json.Serialization;

namespace AarohiBot.Api.Models.Groq;

public class GroqMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user"; // "system", "user", "assistant"

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
