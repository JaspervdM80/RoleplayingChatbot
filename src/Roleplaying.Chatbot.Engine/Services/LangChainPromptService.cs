using Microsoft.Extensions.Logging;
using LangChain.Prompts;
using System.Text.RegularExpressions;

namespace Roleplaying.Chatbot.Engine.Services;

public class LangChainPromptService
{
    private readonly ILogger<LangChainPromptService> _logger;
    private readonly Dictionary<string, string> _promptTemplates = new();

    public LangChainPromptService(ILogger<LangChainPromptService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeTemplatesFromDirectory(string directory)
    {
        try
        {
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
            {
                _logger.LogWarning("Prompt directory {Directory} not found", directory);
                return;
            }

            foreach (var file in dir.GetFiles("*.txt"))
            {
                var templateName = Path.GetFileNameWithoutExtension(file.Name);
                var templateContent = await File.ReadAllTextAsync(file.FullName);

                // Store the raw template content
                _promptTemplates[templateName] = templateContent;
                _logger.LogInformation("Loaded template: {TemplateName}", templateName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading prompt templates");
        }
    }

    public string GetPromptTemplate(string name)
    {
        if (_promptTemplates.TryGetValue(name, out var template))
        {
            return template;
        }

        _logger.LogWarning("Template {TemplateName} not found", name);
        throw new KeyNotFoundException($"Prompt template '{name}' not found");
    }

    public string FormatPrompt(string templateName, Dictionary<string, string> variables)
    {
        try
        {
            var template = GetPromptTemplate(templateName);
            var formattedPrompt = template;

            // Replace variables using Mustache-style syntax
            foreach (var (key, value) in variables)
            {
                formattedPrompt = formattedPrompt.Replace($"{{{{{key}}}}}", value);
            }

            return formattedPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting prompt {TemplateName}", templateName);
            throw;
        }
    }
}
