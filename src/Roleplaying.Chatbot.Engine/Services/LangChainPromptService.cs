using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.RegularExpressions;
using Roleplaying.Chatbot.Engine.Abstractions;

namespace Roleplaying.Chatbot.Engine.Services;

public class LangChainPromptService
{
    private readonly Dictionary<string, LangChainPromptTemplate> _promptTemplates = [];
    private readonly IDeserializer _yamlDeserializer;
    private readonly ILoggingService _loggingService;

    public class LangChainPromptTemplate
    {
        public string Type { get; set; } = "_type: prompt";
        public List<string> InputVariables { get; set; } = [];
        public string Template { get; set; } = string.Empty;
    }

    public LangChainPromptService(ILoggingService loggingService)
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _loggingService = loggingService;
    }

    public async Task InitializeTemplatesFromDirectory(string directory)
    {
        try
        {
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
            {
                _ = _loggingService.LogWarning($"Prompt directory {directory} not found");
                return;
            }

            // Try both .yaml and .txt files
            foreach (var file in dir.GetFiles("*.yaml").Concat(dir.GetFiles("*.txt")))
            {
                var templateName = Path.GetFileNameWithoutExtension(file.Name);
                var templateContent = await File.ReadAllTextAsync(file.FullName);

                try
                {
                    // If it's a YAML file, try to parse it as a LangChain template
                    if (file.Extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase))
                    {
                        var template = _yamlDeserializer.Deserialize<LangChainPromptTemplate>(templateContent);
                        _promptTemplates[templateName] = template;
                    }
                    else
                    {
                        // For .txt files, create a simple template (legacy support)
                        _promptTemplates[templateName] = new LangChainPromptTemplate
                        {
                            Template = templateContent,
                            InputVariables = ExtractVariablesFromTemplate(templateContent)
                        };
                    }
                }
                catch (Exception ex)
                {
                    _ = _loggingService.LogError("InitiliazeTemplates", ex, new { TemplateName = templateName });
                }
            }
        }
        catch (Exception ex)
        {
            _ = _loggingService.LogError("InitiliazeTemplates", ex);
        }
    }

    private List<string> ExtractVariablesFromTemplate(string template)
    {
        var mustachePattern = new Regex(@"{{([^{}]+)}}");
        var bracePattern = new Regex(@"{([^{}]+)}");

        var variables = new HashSet<string>();

        foreach (Match match in mustachePattern.Matches(template))
        {
            if (match.Groups.Count > 1)
            {
                variables.Add(match.Groups[1].Value.Trim());
            }
        }

        foreach (Match match in bracePattern.Matches(template))
        {
            if (match.Groups.Count > 1)
            {
                variables.Add(match.Groups[1].Value.Trim());
            }
        }

        return variables.ToList();
    }

    public string GetPromptTemplate(string name)
    {
        if (_promptTemplates.TryGetValue(name, out var template))
        {
            return template.Template;
        }

        throw new KeyNotFoundException($"Prompt template '{name}' not found");
    }

    public string FormatPrompt(string templateName, Dictionary<string, string> variables)
    {
        try
        {
            if (!_promptTemplates.TryGetValue(templateName, out var templateObj))
            {
                throw new KeyNotFoundException($"Prompt template '{templateName}' not found");
            }

            var formattedPrompt = templateObj.Template;

            // Replace both LangChain {variable} and handlebars {{variable}} style variables
            foreach (var (key, value) in variables)
            {
                // Replace LangChain style {variable}
                formattedPrompt = formattedPrompt.Replace($"{{{key}}}", value);

                // Also replace handlebars style {{variable}} for backwards compatibility
                formattedPrompt = formattedPrompt.Replace($"{{{{{key}}}}}", value);
            }

            return formattedPrompt;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("FormatPrompt", ex, new { TemplateName = templateName });
            throw;
        }
    }
}
