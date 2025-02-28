using System.Text.Json;
using Microsoft.Extensions.Logging;
using Roleplaying.Chatbot.Engine.Models;
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
            string jsonContent = File.ReadAllText(filePath);
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

            _logger.LogInformation("Successfully loaded scenario: {Title}", scenario.ModuleInfo.Name);
            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scenario from {FilePath}", filePath);
            throw new InvalidOperationException($"Error loading scenario: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts a StoryScenario to StoryConfig for use with existing systems
    /// </summary>
    public StoryConfig ConvertToStoryConfig(StoryScenario scenario)
    {
        return scenario.ToStoryConfig();
    }
}
