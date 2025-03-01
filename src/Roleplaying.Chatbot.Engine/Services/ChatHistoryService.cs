using Microsoft.Extensions.Logging;
using Roleplaying.Chatbot.Engine.Models;

namespace Roleplaying.Chatbot.Engine.Services;

public class ChatHistoryService
{
    private readonly ILogger<ChatHistoryService> _logger;
    private readonly Dictionary<string, List<MessageEntry>> _sessionHistories = new();

    public record MessageEntry(string Role, string Content);

    public ChatHistoryService(ILogger<ChatHistoryService> logger)
    {
        _logger = logger;
    }

    public string CreateSession(string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString();
        _sessionHistories[sessionId] = new List<MessageEntry>();
        return sessionId;
    }

    public void AddUserMessage(string sessionId, string content)
    {
        if (!_sessionHistories.TryGetValue(sessionId, out var history))
        {
            sessionId = CreateSession(sessionId);
            history = _sessionHistories[sessionId];
        }

        history.Add(new MessageEntry("user", content));
        _logger.LogDebug("Added user message to session {SessionId}", sessionId);
    }

    public void AddAiMessage(string sessionId, string content)
    {
        if (!_sessionHistories.TryGetValue(sessionId, out var history))
        {
            throw new KeyNotFoundException($"Session '{sessionId}' not found");
        }

        history.Add(new MessageEntry("assistant", content));
        _logger.LogDebug("Added AI message to session {SessionId}", sessionId);
    }

    public void AddAiMessage(string sessionId, StoryInteraction interaction)
    {
        // Convert the structured interaction to a string representation
        var contentBuilder = new System.Text.StringBuilder();

        contentBuilder.AppendLine(interaction.SceneDescription);

        foreach (var charResponse in interaction.CharacterResponses)
        {
            contentBuilder.AppendLine($"{charResponse.CharacterName}: {charResponse.Dialogue}");
            if (!string.IsNullOrEmpty(charResponse.Action))
            {
                contentBuilder.AppendLine($"({charResponse.Action})");
            }
        }

        AddAiMessage(sessionId, contentBuilder.ToString());
    }

    public List<MessageEntry> GetHistory(string sessionId, int maxMessages = 10)
    {
        if (!_sessionHistories.TryGetValue(sessionId, out var history))
        {
            throw new KeyNotFoundException($"Session '{sessionId}' not found");
        }

        // Return the most recent messages, limited by maxMessages
        return history.Skip(Math.Max(0, history.Count - maxMessages)).ToList();
    }

    public string GetFormattedHistory(string sessionId, int maxMessages = 10)
    {
        var history = GetHistory(sessionId, maxMessages);
        var builder = new System.Text.StringBuilder();

        foreach (var message in history)
        {
            string prefix = message.Role == "user" ? "Player" : "AI";
            builder.AppendLine($"[{prefix}]: {message.Content}");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
