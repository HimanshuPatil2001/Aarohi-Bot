using System.Diagnostics;
using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using AarohiBot.Api.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using DbMessage = AarohiBot.Api.Models.Database.Message;
using TelegramMessage = Telegram.Bot.Types.Message;

namespace AarohiBot.Api.Services.Implementations;

public class BotMessagingService : IBotMessagingService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserRepository _userRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserFactRepository _userFactRepository;
    private readonly ILogRepository _logRepository;
    private readonly IPromptBuilderService _promptBuilderService;
    private readonly IGroqService _groqService;
    private readonly IMemoryService _memoryService;
    private readonly IRelationshipEngineService _relationshipEngineService;
    private readonly IConfiguration _config;
    private readonly ILogger<BotMessagingService> _logger;

    public BotMessagingService(
        ITelegramBotClient botClient,
        IUserRepository userRepository,
        IMessageRepository messageRepository,
        IUserFactRepository userFactRepository,
        ILogRepository logRepository,
        IPromptBuilderService promptBuilderService,
        IGroqService groqService,
        IMemoryService memoryService,
        IRelationshipEngineService relationshipEngineService,
        IConfiguration config,
        ILogger<BotMessagingService> logger)
    {
        _botClient = botClient;
        _userRepository = userRepository;
        _messageRepository = messageRepository;
        _userFactRepository = userFactRepository;
        _logRepository = logRepository;
        _promptBuilderService = promptBuilderService;
        _groqService = groqService;
        _memoryService = memoryService;
        _relationshipEngineService = relationshipEngineService;
        _config = config;
        _logger = logger;
    }

    public async Task HandleIncomingMessageAsync(TelegramMessage message)
    {
        if (message.From == null || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var telegramUserId = message.From.Id;
        var chatId = message.Chat.Id;
        var userText = message.Text.Trim();
        long? currentUserId = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Send Telegram typing indicator
            try
            {
                await _botClient.SendChatActionAsync(chatId: chatId, chatAction: ChatAction.Typing);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send typing chat action to Telegram ChatId {ChatId}", chatId);
            }

            // 2. Resolve or create user profile
            var user = await _userRepository.GetByTelegramIdAsync(telegramUserId);
            if (user == null)
            {
                user = new User
                {
                    TelegramId = telegramUserId,
                    FirstName = message.From.FirstName,
                    LastName = message.From.LastName,
                    Username = message.From.Username,
                    LanguagePreference = "Hinglish",
                    RelationshipTier = "Acquaintance",
                    IsActive = true,
                    OnboardingStep = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastActiveAt = DateTimeOffset.UtcNow
                };

                user.UserId = await _userRepository.CreateUserAsync(user);

                await _logRepository.LogActivityAsync(new ActivityLog
                {
                    UserId = user.UserId,
                    TelegramId = telegramUserId,
                    ActionType = "NewUserRegistered",
                    Description = $"New user registered: {user.FirstName} (@{user.Username})",
                    IsSuccess = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                await _userRepository.UpdateLastActiveAsync(user.UserId);
            }

            currentUserId = user.UserId;

            // 3. Save incoming user message to database
            await _messageRepository.SaveMessageAsync(new DbMessage
            {
                UserId = user.UserId,
                Sender = "User",
                Content = userText,
                TokensUsed = 0,
                ModelUsed = null,
                CreatedAt = DateTimeOffset.UtcNow
            });

            // 4. Retrieve chat history and stored user facts
            var chatHistory = await _messageRepository.GetRecentChatHistoryAsync(user.UserId, limit: 10);
            var facts = await _userFactRepository.GetFactsByUserIdAsync(user.UserId);

            // 5. Process Relationship Engine & Milestones
            var (relState, milestone) = await _relationshipEngineService.ProcessMessageAndEvaluateStateAsync(user.UserId, userText, facts.Count);

            // 6. Build system prompt and conversational payload with relationship state
            var promptMessages = _promptBuilderService.BuildPrompt(user, relState, facts, chatHistory, userText);

            // 7. Generate reply from Groq
            var replyText = await _groqService.GenerateResponseAsync(promptMessages, useDeepModel: false);

            // 8. Append Milestone Unlock Banner if earned
            if (milestone != null)
            {
                replyText += $"\n\n✨ *[{milestone.Title}]*\n_{milestone.Description}_";
            }

            // 9. Send reply to Telegram (with fallback if markdown formatting has special chars)
            try
            {
                await _botClient.SendTextMessageAsync(chatId: chatId, text: replyText, parseMode: ParseMode.Markdown);
            }
            catch (Exception)
            {
                await _botClient.SendTextMessageAsync(chatId: chatId, text: replyText);
            }

            stopwatch.Stop();

            // 10. Save bot message to database
            await _messageRepository.SaveMessageAsync(new DbMessage
            {
                UserId = user.UserId,
                Sender = "Bot",
                Content = replyText,
                TokensUsed = 0,
                ModelUsed = _config["Groq:FastModel"] ?? "llama-3.1-8b-instant",
                CreatedAt = DateTimeOffset.UtcNow
            });

            // 11. Log user activity audit trail
            await _logRepository.LogActivityAsync(new ActivityLog
            {
                UserId = user.UserId,
                TelegramId = telegramUserId,
                ActionType = "ChatMessageTurn",
                Description = $"User sent message: '{userText}'. Bot replied: '{replyText}' [Stage: {relState.CurrentStage}]",
                DurationMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

            // 12. Asynchronously extract facts from user message in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _memoryService.ExtractAndSaveFactsAsync(user.UserId, userText);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background memory extraction failed for UserId {UserId}", user.UserId);
                    await _logRepository.LogErrorAsync(new ErrorLog
                    {
                        UserId = user.UserId,
                        TelegramId = telegramUserId,
                        Source = "MemoryService",
                        UserAction = $"Extracting facts from user message: '{userText}'",
                        ErrorMessage = ex.Message,
                        ExceptionType = ex.GetType().FullName,
                        StackTrace = ex.StackTrace,
                        InnerException = ex.InnerException?.ToString(),
                        RequestData = userText,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling incoming Telegram message from TelegramId {TelegramId}", telegramUserId);

            // Record full error trace to ErrorLogs table
            await _logRepository.LogErrorAsync(new ErrorLog
            {
                UserId = currentUserId,
                TelegramId = telegramUserId,
                Source = "BotMessagingService",
                UserAction = $"Sending message to Aarohi: '{userText}'",
                ErrorMessage = ex.Message,
                ExceptionType = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.ToString(),
                RequestData = userText,
                CreatedAt = DateTimeOffset.UtcNow
            });

            // Record failed activity
            await _logRepository.LogActivityAsync(new ActivityLog
            {
                UserId = currentUserId,
                TelegramId = telegramUserId,
                ActionType = "ChatMessageFailed",
                Description = $"Failed to process message: '{userText}'. Error: {ex.Message}",
                DurationMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = false,
                CreatedAt = DateTimeOffset.UtcNow
            });

            try
            {
                await _botClient.SendTextMessageAsync(chatId: chatId, text: "Arey yaar, thoda sa technical issue aa gaya. Ek second baad dubara message kar na please?");
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "Failed to send error fallback message to ChatId {ChatId}", chatId);
            }
        }
    }
}
