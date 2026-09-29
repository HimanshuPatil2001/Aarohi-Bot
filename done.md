# 🚀 Aarohi AI Companion - Project Accomplishments & Milestone Summary

This document summarizes all components, architecture layers, features, and fixes achieved for the **Aarohi AI Companion Backend API** (.NET 8, Telegram Webhook, Groq LLM, MS SQL Server, Dapper).

---

## 🏗️ 1. Architecture & Project Structuring
- **Clean Layered Solution**: Reorganized the project into modular layers: `Controllers/`, `Services/`, `Repositories/`, `Models/`, and `Database/`.
- **Package Management (`aarohi.csproj`)**:
  - `Dapper` (2.1.35) — High-performance raw SQL data mapping.
  - `Microsoft.Data.SqlClient` (5.2.2) — ADO.NET SQL Server connection provider.
  - `Telegram.Bot` (19.0.0) — Official Telegram Bot API library.
  - `Microsoft.AspNetCore.Mvc.NewtonsoftJson` (8.0.8) — Resolves polymorphic JSON deserialization for Telegram `Update` payloads.
  - `Swashbuckle.AspNetCore` (6.6.2) — OpenAPI/Swagger documentation.
- **Dependency Injection (`Program.cs`)**: Complete wiring of all repositories, typed HTTP clients, singleton `TelegramBotClient`, services, and JSON serialization options.

---

## 🗄️ 2. Database Schema & Data Access Layer (`[companion Db]`)
Created and maintained the complete T-SQL database schema in [`Database/schema.sql`](Database/schema.sql) with 6 optimized tables, indexes, and cascade constraints:

1. **`dbo.Users`**: Manages user profiles, Telegram IDs, language preferences, relationship tiers, and onboarding progression.
2. **`dbo.Messages`**: Stores complete chat history with sender role (`User`/`Bot`), token counts, and model identifiers.
3. **`dbo.UserFacts`**: Relational context memory storing facts learned about each user (e.g. City, Job, Hobbies, Stress triggers).
4. **`dbo.ScheduledNotifications`**: Queue for automated daily check-ins (Good Morning, Good Night) ready for n8n / cron automation.
5. **`dbo.ErrorLogs`**: Exception tracker capturing full stack traces, inner exceptions, component source, and what the user was doing.
6. **`dbo.ActivityLogs`**: User and bot interaction audit trail recording response durations (`DurationMs`), action types, and success flags.

### Dapper Repositories:
- `UserRepository` & `IUserRepository`
- `MessageRepository` & `IMessageRepository`
- `UserFactRepository` & `IUserFactRepository`
- `ScheduledNotificationRepository` & `IScheduledNotificationRepository`
- `LogRepository` & `ILogRepository` (Resilient, non-blocking logging)

---

## 🤖 3. AI Inference & Dynamic Persona Engine
- **Groq LLM Service (`GroqService.cs`)**:
  - Typed HTTP client interfacing with Groq's OpenAI-compatible completions API (`https://api.groq.com/openai/v1/`).
  - Supports dynamic model routing (`llama-3.1-8b-instant` for ultra-fast chat turns; `llama-3.3-70b-versatile` for deeper reasoning).
  - Built-in error fallback handling with friendly Hinglish responses.
- **Dynamic Prompt Builder (`PromptBuilderService.cs`)**:
  - Implements Aarohi's core persona: 23-year-old creative marketing professional in Bengaluru, warm, witty, and empathetic.
  - **The 15-Message Hook Strategy**:
    - *Turns 1–3*: Warmth, informal greetings, low friction.
    - *Turns 4–7*: Active listening, validating work/study stress.
    - *Turns 8–11*: Playful teasing (*nautanki*, *champu*), shared everyday snippets (Bengaluru traffic, cold coffee).
    - *Turns 12–15+*: Emotional connection, nicknames, referencing stored facts.
  - **Persona Fine-Tuning**: Explicitly removed repetitive "re" filler slang and enforced consistent informal pronouns (`tum / tumhara`).
- **Context Memory Service (`MemoryService.cs`)**:
  - Background extraction of newly disclosed personal facts from chat messages and upserting into `dbo.UserFacts`.

---

## ⚡ 4. Bot Orchestration & Webhooks
- **`BotMessagingService.cs`**:
  - Handles incoming Telegram messages end-to-end:
    1. Sends Telegram **"Typing..."** chat action.
    2. Resolves or creates user profile in `dbo.Users`.
    3. Saves user message to `dbo.Messages`.
    4. Fetches recent 10 messages & memorized facts.
    5. Builds prompt and calls Groq AI.
    6. Sends response back to Telegram.
    7. Saves bot reply to `dbo.Messages` and logs activity duration in `dbo.ActivityLogs`.
    8. Triggers background fact extraction.
- **`TelegramController.cs`**:
  - `POST /api/telegram/webhook`: Endpoint with optional `X-Telegram-Bot-Api-Secret-Token` validation.
  - `GET /api/telegram/health`: Health check endpoint.
- **`NotificationsController.cs`**:
  - `POST /api/notifications/process-pending`: Dispatches scheduled retention notifications.
  - `POST /api/notifications/schedule`: Endpoint to enqueue future notifications.

---

## 🔧 5. Debugging & Verification Milestones
- **400 Bad Request Resolution**: Diagnosed and fixed the polymorphic `Telegram.Bot.Types.Update` model binding failure using `AddNewtonsoftJson()`.
- **Visual Studio Dev Tunnels**: Successfully configured and exposed local port (`7259`) via public tunnel `aarohi-bot` (`https://rzl5xnfd-7259.inc1.devtunnels.ms`).
- **Live Telegram Webhook Test**: Verified live chat turns on Telegram with real-time AI responses, playful banter, and database persistence.

---

## 🐳 6. Containerization & TrueNAS Hosting
- **`Dockerfile`**: Multi-stage .NET 8 container build optimized for production.
- **`docker-compose.yml`**: Docker Compose deployment configuration with all environment variables mapped.
- **Hosting Guide**: Documented complete setup for deploying on **TrueNAS SCALE** and exposing via **Cloudflare Tunnels** (free HTTPS, no router port forwarding).

---

## 📊 7. Useful SQL Monitoring Queries

```sql
USE [companion Db];
GO

-- 1. Check Error Logs
SELECT TOP 20 ErrorLogId, CreatedAt, Source, UserAction, ErrorMessage, StackTrace, RequestData 
FROM dbo.ErrorLogs ORDER BY CreatedAt DESC;

-- 2. Check Activity Audit Logs
SELECT TOP 20 ActivityLogId, CreatedAt, ActionType, Description, DurationMs, IsSuccess 
FROM dbo.ActivityLogs ORDER BY CreatedAt DESC;

-- 3. Check Chat History
SELECT TOP 20 m.CreatedAt, u.FirstName, m.Sender, m.Content, m.ModelUsed 
FROM dbo.Messages m JOIN dbo.Users u ON m.UserId = u.UserId ORDER BY m.CreatedAt DESC;

-- 4. Check Extracted User Facts
SELECT u.FirstName, f.FactCategory, f.FactValue, f.CreatedAt 
FROM dbo.UserFacts f JOIN dbo.Users u ON f.UserId = u.UserId ORDER BY f.CreatedAt DESC;
```
