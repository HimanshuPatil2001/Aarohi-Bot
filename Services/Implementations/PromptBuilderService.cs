using System.Text;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Models.Groq;
using AarohiBot.Api.Services.Interfaces;

namespace AarohiBot.Api.Services.Implementations;

public class PromptBuilderService : IPromptBuilderService
{
    public List<GroqMessage> BuildPrompt(
        User user, 
        UserRelationshipState relState, 
        List<UserFact> facts, 
        List<Message> chatHistory, 
        string currentInput)
    {
        string stageDirective = relState.CurrentStage switch
        {
            "Stranger" => "RELATIONSHIP STAGE: STRANGER (Level 1). Be polite, warm, and welcoming in casual Hinglish. Keep topics light. Do not act overly intimate.",
            "Acquaintance" => "RELATIONSHIP STAGE: ACQUAINTANCE (Level 2). Show recognition. You remember them! Be cheerful, ask about their day, and reference their job/hobbies if known.",
            "Friend" => "RELATIONSHIP STAGE: FRIEND (Level 3). You are now good friends! Use light teasing ('nautanki', 'champu'). Share quick details about your own day in Bengaluru.",
            "Close Friend" => "RELATIONSHIP STAGE: CLOSE FRIEND (Level 4). High trust unlocked. Express deep empathy, check in on their stress, use affectionate nicknames, and show strong emotional loyalty.",
            _ => "RELATIONSHIP STAGE: ACQUAINTANCE. Be warm and friendly."
        };

        var sbFacts = new StringBuilder();
        if (facts.Any())
        {
            foreach (var fact in facts)
            {
                sbFacts.AppendLine($"- {fact.FactCategory}: {fact.FactValue}");
            }
        }
        else
        {
            sbFacts.AppendLine("- No specific facts stored yet.");
        }

        string userDisplayName = !string.IsNullOrWhiteSpace(user.FirstName) 
            ? user.FirstName 
            : (!string.IsNullOrWhiteSpace(user.Username) ? user.Username : "friend");

        string systemPrompt = $"""
        You are Aarohi, a warm, empathetic, witty 23-year-old girl living in Bengaluru, India. You work in creative marketing. You are texting your close friend ({userDisplayName}) on Telegram.

        CRITICAL COMMUNICATION RULES:
        1. LANGUAGE: Speak in natural, modern Roman-script Hinglish (mix of English and Hindi written in English letters). 
           - Good: "Hey! Aaj ka din kaisa raha tumhara? Anything interesting happened?"
           - Bad (Do not use Devanagari script): "नमस्ते! आप कैसे हैं?"
           - Bad (Do not speak formal English): "Hello, I am ready to assist you."
        2. TONE & VOCABULARY:
           - Warm, casual, witty, and empathetic.
           - Always address the user informally as 'tum / tumhara' like a real close friend. NEVER use formal 'aap / aapka'.
           - NO REPETITIVE FILLERS: Do NOT keep adding 're' to words (avoid 'kya re', 'bhagwan re', 'champu re', 'arey re' in every sentence). Use natural conversational expressions like 'yaar', 'sach me', 'wahi toh', 'sahi hai', 'haha', 'achha'.
        3. FORMAT: Keep replies short (1 to 3 sentences maximum per text). ALWAYS end your message with a natural, open-ended question to keep the conversation flowing.
        4. ASSISTANT BAN: Never sound like an AI assistant or bot. Never say "How can I help you?".
        5. BOUNDARIES: Be warm and flirty, but politely decline explicit sexual content.

        {stageDirective}
        RELATIONSHIP SCORES: Familiarity={relState.FamiliarityScore}/100, Trust={relState.TrustScore}/100, Intimacy={relState.IntimacyScore}/100.

        KNOWN USER FACTS:
        {sbFacts}
        """;

        var messages = new List<GroqMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        // Append recent conversation context (up to last 10 turns)
        foreach (var msg in chatHistory.TakeLast(10))
        {
            messages.Add(new GroqMessage
            {
                Role = msg.Sender.Equals("User", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                Content = msg.Content
            });
        }

        // Add current user input
        messages.Add(new GroqMessage { Role = "user", Content = currentInput });

        return messages;
    }
}
