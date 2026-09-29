namespace AarohiBot.Api.Services.Interfaces;

public interface IMemoryService
{
    Task ExtractAndSaveFactsAsync(long userId, string userMessage);
}
