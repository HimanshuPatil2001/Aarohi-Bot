using AarohiBot.Api.Models.Database;
using AarohiBot.Api.Repositories.Interfaces;
using AarohiBot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace AarohiBot.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController : ControllerBase
{
    private readonly IBotMessagingService _messagingService;
    private readonly ILogRepository _logRepository;
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        IBotMessagingService messagingService, 
        ILogRepository logRepository,
        IConfiguration config,
        ILogger<TelegramController> logger)
    {
        _messagingService = messagingService;
        _logRepository = logRepository;
        _config = config;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleUpdate([FromBody] Update update)
    {
        // Optional webhook secret verification
        var expectedSecret = _config["Telegram:WebhookSecretToken"];
        if (!string.IsNullOrWhiteSpace(expectedSecret))
        {
            if (Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secret))
            {
                if (secret != expectedSecret)
                {
                    _logger.LogWarning("Unauthorized webhook request with invalid secret token.");
                    await _logRepository.LogErrorAsync(new ErrorLog
                    {
                        Source = "TelegramController",
                        UserAction = "WebhookAuthentication",
                        ErrorMessage = "Unauthorized webhook request: Secret token mismatch.",
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    return Unauthorized();
                }
            }
            else
            {
                _logger.LogWarning("Webhook request missing X-Telegram-Bot-Api-Secret-Token header.");
                await _logRepository.LogErrorAsync(new ErrorLog
                {
                    Source = "TelegramController",
                    UserAction = "WebhookAuthentication",
                    ErrorMessage = "Unauthorized webhook request: Missing secret token header.",
                    CreatedAt = DateTimeOffset.UtcNow
                });
                return Unauthorized();
            }
        }

        try
        {
            if (update.Message is { Text: not null } message)
            {
                await _messagingService.HandleIncomingMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Telegram webhook controller.");
            await _logRepository.LogErrorAsync(new ErrorLog
            {
                TelegramId = update.Message?.From?.Id,
                Source = "TelegramController",
                UserAction = $"Processing Webhook Update ID {update.Id}",
                ErrorMessage = ex.Message,
                ExceptionType = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return Ok();
    }

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "Healthy", bot = "Aarohi AI Companion", time = DateTimeOffset.UtcNow });
    }
}
