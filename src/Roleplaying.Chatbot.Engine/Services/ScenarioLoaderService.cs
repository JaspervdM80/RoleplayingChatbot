using System.Text.Json;
using Microsoft.Extensions.Logging;
using Roleplaying.Chatbot.Engine.Models.Story;

namespace Roleplaying.Chatbot.Engine.Services;

public class ScenarioLoaderService
{
    private readonly ILogger<ScenarioLoaderService> _logger;

    public ScenarioLoaderService(ILogger<ScenarioLoaderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads a story scenario from a JSON file
    /// </summary>
    public StoryScenario LoadFromFile(string filePath)
    {
        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var scenario = JsonSerializer.Deserialize<StoryScenario>(jsonContent, options);

            if (scenario == null)
            {
                _logger.LogError("Failed to deserialize scenario from {FilePath}", filePath);
                throw new InvalidOperationException($"Failed to load scenario from {filePath}");
            }

            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scenario from {FilePath}", filePath);
            throw new InvalidOperationException($"Error loading scenario: {ex.Message}", ex);
        }
    }
}
