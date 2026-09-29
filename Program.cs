using AarohiBot.Api.Repositories.Implementations;
using AarohiBot.Api.Repositories.Interfaces;
using AarohiBot.Api.Services.Implementations;
using AarohiBot.Api.Services.Interfaces;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with NewtonsoftJson for Telegram.Bot polymorphic type support
builder.Services.AddControllers()
    .AddNewtonsoftJson();

// Configure Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Aarohi AI Companion API", Version = "v1", Description = "Backend API for Aarohi Telegram AI Companion Bot" });
});

// Register Telegram Bot Client
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var token = builder.Configuration["Telegram:BotToken"] ?? string.Empty;
    return new TelegramBotClient(token);
});

// Register Repositories (Dapper)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IUserFactRepository, UserFactRepository>();
builder.Services.AddScoped<IScheduledNotificationRepository, ScheduledNotificationRepository>();
builder.Services.AddScoped<ILogRepository, LogRepository>();

// Register Services
builder.Services.AddHttpClient<IGroqService, GroqService>();
builder.Services.AddScoped<IRelationshipEngineService, RelationshipEngineService>();
builder.Services.AddScoped<IPromptBuilderService, PromptBuilderService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();
builder.Services.AddScoped<IBotMessagingService, BotMessagingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aarohi API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
