namespace Roleplaying.Chatbot.Engine.Abstractions;

public interface ILoggingService
{
    Task LogError(string operation, Exception exception, object? context = null);
    Task LogWarning(string operation, Exception? exception = null, object? context = null);
    Task LogLlmInteraction(string promptName, string request, string response, TimeSpan duration);
}
