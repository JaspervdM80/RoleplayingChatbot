using System.Text.Json;
using Microsoft.Extensions.Logging;
using Roleplaying.Chatbot.Engine.Abstractions;

namespace Roleplaying.Chatbot.Engine.Services;

public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly string _logDirectory;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger;

        // Create logs directory in the application root
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public async Task LogLlmInteraction(string promptName, string request, string response, TimeSpan duration)
    {
        try
        {
            // Create a unique filename with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var filename = Path.Combine(_logDirectory, $"llm_{promptName}_{timestamp}.log");

            var logContent = new
            {
                Timestamp = DateTime.UtcNow,
                PromptName = promptName,
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Request = request,
                Response = response
            };

            // Serialize to JSON for better readability
            var json = JsonSerializer.Serialize(logContent, _jsonOptions);
            await File.WriteAllTextAsync(filename, json);

            _logger.LogInformation("Logged LLM interaction to {Filename}, duration: {DurationMs}ms",
                filename, Math.Round(duration.TotalMilliseconds, 2));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log LLM interaction");
        }
    }

    public async Task LogError(string operation, Exception exception, object? context = null)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var filename = Path.Combine(_logDirectory, $"error_{timestamp}.log");

            var logContent = new
            {
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                Exception = new
                {
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException?.Message
                },
                Context = context
            };

            var json = JsonSerializer.Serialize(logContent, _jsonOptions);
            await File.WriteAllTextAsync(filename, json);

            _logger.LogError("Logged error details to {Filename}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error details");
        }
    }

    public async Task LogWarning(string operation, Exception? exception = null, object? context = null)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var filename = Path.Combine(_logDirectory, $"warning_{timestamp}.log");

            var logContent = new
            {
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                Exception =
                    exception != null
                        ?  new {
                            Message = exception.Message,
                            StackTrace = exception.StackTrace,
                            InnerException = exception.InnerException?.Message
                        }
                        : null,
                Context = context
            };

            var json = JsonSerializer.Serialize(logContent, _jsonOptions);
            await File.WriteAllTextAsync(filename, json);

            _logger.LogWarning("Logged warning details to {Filename}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error details");
        }
    }
}
