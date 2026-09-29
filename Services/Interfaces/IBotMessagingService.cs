using Telegram.Bot.Types;

namespace AarohiBot.Api.Services.Interfaces;

public interface IBotMessagingService
{
    Task HandleIncomingMessageAsync(Message message);
}
