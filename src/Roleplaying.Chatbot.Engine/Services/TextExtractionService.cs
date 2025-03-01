using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Roleplaying.Chatbot.Engine.Models;

namespace Roleplaying.Chatbot.Engine.Services;

/// <summary>
/// Service for extracting structured information from natural language text responses
/// </summary>
public class TextExtractionService
{
    private readonly ILogger<TextExtractionService> _logger;
    private readonly Kernel _kernel;

    public TextExtractionService(ILogger<TextExtractionService> logger, Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;
    }

    /// <summary>
    /// Extracts a structured StoryInteraction from a natural language text response
    /// </summary>
    public async Task<StoryInteraction> ExtractStoryInteractionAsync(string textResponse, string playerAction)
    {
        var storyInteraction = new StoryInteraction
        {
            PlayerAction = playerAction,
            CharacterResponses = new List<CharacterResponse>()
        };

        try
        {
            // Extract scene description (usually the first paragraph)
            storyInteraction.SceneDescription = ExtractSceneDescription(textResponse);

            // Extract character responses
            storyInteraction.CharacterResponses = ExtractCharacterResponses(textResponse);

            // Extract location
            storyInteraction.Location = ExtractLocation(textResponse);

            // Extract plot developments
            storyInteraction.PlotDevelopments = ExtractPlotDevelopments(textResponse);

            // Extract relationship changes
            storyInteraction.RelationshipChanges = ExtractRelationshipChanges(textResponse);

            // If we couldn't extract anything meaningful, use AI to help
            if (string.IsNullOrWhiteSpace(storyInteraction.SceneDescription) ||
                !storyInteraction.CharacterResponses.Any())
            {
                await EnhanceExtractionWithAIAsync(textResponse, storyInteraction);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting story interaction from text response");

            // Ensure we return something even if extraction fails
            if (string.IsNullOrWhiteSpace(storyInteraction.SceneDescription))
            {
                storyInteraction.SceneDescription = textResponse;
            }
        }

        return storyInteraction;
    }

    private string ExtractSceneDescription(string text)
    {
        // The scene description is usually the first paragraph or two before any character dialogue
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Look for paragraphs that don't contain character dialogue (which typically starts with a name followed by a colon)
        var dialoguePattern = new Regex(@"^[A-Z][a-zA-Z\s]+:", RegexOptions.Multiline);

        var sceneDescriptionParagraphs = new List<string>();

        foreach (var paragraph in paragraphs)
        {
            // If this paragraph contains dialogue, we've reached the end of the scene description
            if (dialoguePattern.IsMatch(paragraph))
            {
                break;
            }

            sceneDescriptionParagraphs.Add(paragraph);
        }

        return string.Join("\n\n", sceneDescriptionParagraphs).Trim();
    }

    private List<CharacterResponse> ExtractCharacterResponses(string text)
    {
        var responses = new List<CharacterResponse>();

        // Pattern to match character dialogue: "Character name: Their dialogue"
        var dialoguePattern = new Regex(@"([A-Z][a-zA-Z\s]+?):\s+(.+?)(?=\n[A-Z][a-zA-Z\s]+?:|$)",
            RegexOptions.Singleline);

        // Pattern to match actions in parentheses
        var actionPattern = new Regex(@"\(([^)]+)\)");

        var matches = dialoguePattern.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var characterName = match.Groups[1].Value.Trim();
                var fullDialogue = match.Groups[2].Value.Trim();

                // Extract actions from parentheses
                var actionMatches = actionPattern.Matches(fullDialogue);
                var actions = new List<string>();

                foreach (Match actionMatch in actionMatches)
                {
                    actions.Add(actionMatch.Groups[1].Value);
                }

                // Remove actions from dialogue
                var dialogue = actionPattern.Replace(fullDialogue, "").Trim();

                // Extract emotions if present (often in form "Character name (emotion): dialogue")
                var emotion = "";
                var emotionMatch = Regex.Match(characterName, @"(.*?)\s*\((.*?)\)");
                if (emotionMatch.Success)
                {
                    characterName = emotionMatch.Groups[1].Value.Trim();
                    emotion = emotionMatch.Groups[2].Value.Trim();
                }

                responses.Add(new CharacterResponse
                {
                    CharacterName = characterName,
                    Dialogue = dialogue,
                    Action = string.Join("; ", actions),
                    Emotion = emotion
                });
            }
        }

        return responses;
    }

    private string ExtractLocation(string text)
    {
        // Look for location indicators
        var locationPatterns = new[]
        {
            new Regex(@"in\s+the\s+([a-zA-Z0-9\s']+)", RegexOptions.IgnoreCase),
            new Regex(@"at\s+the\s+([a-zA-Z0-9\s']+)", RegexOptions.IgnoreCase),
            new Regex(@"inside\s+the\s+([a-zA-Z0-9\s']+)", RegexOptions.IgnoreCase),
            new Regex(@"location:\s*([a-zA-Z0-9\s']+)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in locationPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // No explicit location found
        return string.Empty;
    }

    private List<string> ExtractPlotDevelopments(string text)
    {
        var developments = new List<string>();

        // Look for narrative indicators
        var narrativePatterns = new[]
        {
            new Regex(@"The\s+story\s+progresses\s+as\s+([^.!?]+)[.!?]", RegexOptions.IgnoreCase),
            new Regex(@"A\s+new\s+development\s+([^.!?]+)[.!?]", RegexOptions.IgnoreCase),
            new Regex(@"The\s+plot\s+thickens\s+as\s+([^.!?]+)[.!?]", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in narrativePatterns)
        {
            var matches = pattern.Matches(text);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    developments.Add(match.Groups[1].Value.Trim());
                }
            }
        }

        return developments;
    }

    private List<RelationshipChange> ExtractRelationshipChanges(string text)
    {
        var changes = new List<RelationshipChange>();

        // Pattern to match relationship changes
        var relationshipPatterns = new[]
        {
            new Regex(@"([A-Z][a-zA-Z\s]+) and ([A-Z][a-zA-Z\s]+) (?:are now|become|grow) ([a-zA-Z\s]+)",
                RegexOptions.IgnoreCase),
            new Regex(@"The relationship between ([A-Z][a-zA-Z\s]+) and ([A-Z][a-zA-Z\s]+) ([^.!?]+)[.!?]",
                RegexOptions.IgnoreCase)
        };

        foreach (var pattern in relationshipPatterns)
        {
            var matches = pattern.Matches(text);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 3)
                {
                    changes.Add(new RelationshipChange
                    {
                        Character1 = match.Groups[1].Value.Trim(),
                        Character2 = match.Groups[2].Value.Trim(),
                        Change = match.Groups[3].Value.Trim(),
                        Reason = "" // We'll need AI to extract the reason
                    });
                }
            }
        }

        return changes;
    }

    /// <summary>
    /// Uses the LLM to enhance extraction when rule-based extraction doesn't yield good results
    /// </summary>
    private async Task EnhanceExtractionWithAIAsync(string textResponse, StoryInteraction storyInteraction)
    {
        try
        {
            // Create a prompt to extract structured information
            var extractionPrompt = @"
Extract structured information from this story response. Do not add any new content.

TEXT:
{{$text}}

Extract and provide ONLY the following information in this format:

Scene Description: [The main narrative description of what's happening]

Characters and their responses:
- Character 1: [Name]
  Dialogue: [What they say]
  Action: [What they do, if mentioned]
  Emotion: [Their emotional state, if mentioned]
- Character 2: [Name]
  Dialogue: [What they say]
  Action: [What they do, if mentioned]
  Emotion: [Their emotional state, if mentioned]

Location: [Where this scene takes place]

Plot Developments: [Any significant story developments, separated by semicolons]

Relationship Changes: [Any changes in relationships between characters, formatted as 'Character1 and Character2: nature of change']
";
            var function = _kernel.CreateFunctionFromPrompt(extractionPrompt);
            var arguments = new KernelArguments
            {

                ["text"] = textResponse
            };

            var result = await _kernel.InvokeAsync(function, arguments);


            var extractedInfo = result.ToString();

            // Parse the LLM's structured response
            if (!string.IsNullOrEmpty(extractedInfo))
            {
                // Get scene description
                var sceneMatch = Regex.Match(extractedInfo, @"Scene Description:\s*(.+?)(?=\n\n|$)",
                    RegexOptions.Singleline);
                if (sceneMatch.Success && string.IsNullOrWhiteSpace(storyInteraction.SceneDescription))
                {
                    storyInteraction.SceneDescription = sceneMatch.Groups[1].Value.Trim();
                }

                // Get location
                var locationMatch = Regex.Match(extractedInfo, @"Location:\s*(.+?)(?=\n\n|$)");
                if (locationMatch.Success && string.IsNullOrWhiteSpace(storyInteraction.Location))
                {
                    storyInteraction.Location = locationMatch.Groups[1].Value.Trim();
                }

                // Get character responses
                var characterSection = Regex.Match(extractedInfo,
                    @"Characters and their responses:(.*?)(?=\n\nLocation:|$)", RegexOptions.Singleline);

                if (characterSection.Success && !storyInteraction.CharacterResponses.Any())
                {
                    var characterBlocks = Regex.Matches(characterSection.Groups[1].Value,
                        @"- (.+?)(?=\n- |\n\n|$)", RegexOptions.Singleline);

                    foreach (Match charBlock in characterBlocks)
                    {
                        var nameMatch = Regex.Match(charBlock.Value, @"([A-Z][a-zA-Z\s]+):");
                        var dialogueMatch = Regex.Match(charBlock.Value, @"Dialogue:\s*(.+?)(?=\n\s+Action:|$)",
                            RegexOptions.Singleline);
                        var actionMatch = Regex.Match(charBlock.Value, @"Action:\s*(.+?)(?=\n\s+Emotion:|$)",
                            RegexOptions.Singleline);
                        var emotionMatch = Regex.Match(charBlock.Value, @"Emotion:\s*(.+?)(?=\n|$)");

                        if (nameMatch.Success)
                        {
                            storyInteraction.CharacterResponses.Add(new CharacterResponse
                            {
                                CharacterName = nameMatch.Groups[1].Value.Trim(),
                                Dialogue = dialogueMatch.Success ? dialogueMatch.Groups[1].Value.Trim() : "",
                                Action = actionMatch.Success ? actionMatch.Groups[1].Value.Trim() : "",
                                Emotion = emotionMatch.Success ? emotionMatch.Groups[1].Value.Trim() : ""
                            });
                        }
                    }
                }

                // Get plot developments
                var plotMatch = Regex.Match(extractedInfo, @"Plot Developments:\s*(.+?)(?=\n\n|$)");
                if (plotMatch.Success && !storyInteraction.PlotDevelopments.Any())
                {
                    storyInteraction.PlotDevelopments = plotMatch.Groups[1].Value
                        .Split(';')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }

                // Get relationship changes
                var relationshipMatch = Regex.Match(extractedInfo, @"Relationship Changes:\s*(.+?)(?=\n\n|$)");
                if (relationshipMatch.Success && !storyInteraction.RelationshipChanges.Any())
                {
                    var relationshipChanges = relationshipMatch.Groups[1].Value.Split(';');
                    foreach (var change in relationshipChanges)
                    {
                        var parts = change.Split(':');
                        if (parts.Length >= 2)
                        {
                            var characters = parts[0].Split(" and ");
                            if (characters.Length >= 2)
                            {
                                storyInteraction.RelationshipChanges.Add(new RelationshipChange
                                {
                                    Character1 = characters[0].Trim(),
                                    Character2 = characters[1].Trim(),
                                    Change = parts[1].Trim(),
                                    Reason = ""
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing extraction with AI");
        }
    }
}
