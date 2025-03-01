using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Roleplaying.Chatbot.Engine.Models;
using Roleplaying.Chatbot.Engine.Services;
using Roleplaying.Chatbot.Engine.Settings;

namespace Roleplaying.Chatbot.Engine;

/// <summary>
/// Main application for the interactive story
/// </summary>
public class InteractiveStoryApp
{
    private readonly StoryService _memoryService;
    private readonly Kernel _kernel;
    private readonly LangChainPromptService _promptService;
    private readonly ILogger<InteractiveStoryApp> _logger;
    private readonly StoryConfig _storyConfig;
    private readonly ChatHistoryService _chatHistoryService;
    private readonly TextExtractionService _textExtractionService;
    private string _sessionId;

    public InteractiveStoryApp(
        StoryService memoryService,
        Kernel kernel,
        LangChainPromptService promptService,
        ChatHistoryService chatHistoryService,
        TextExtractionService textExtractionService,
        ILogger<InteractiveStoryApp> logger,
        StoryConfig storyConfig)
    {
        _memoryService = memoryService;
        _kernel = kernel;
        _promptService = promptService;
        _logger = logger;
        _storyConfig = storyConfig;
        _chatHistoryService = chatHistoryService;
        _textExtractionService = textExtractionService;
        _sessionId = _chatHistoryService.CreateSession();
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting interactive story: {Title}", _storyConfig.Title);

        // Initial story setup - create first scene
        Console.WriteLine(_storyConfig.Setting);
        Console.WriteLine("\n--- STORY BEGINS ---\n");

        // If this is first run, create an initial scene
        var initialStoryResponse = await CreateInitialSceneAsync();
        await ProcessAndDisplayResponse(initialStoryResponse);

        // Main interaction loop
        while (true)
        {
            Console.Write("\nWhat would you like to say or do? (type 'exit' to quit): ");
            var playerInput = Console.ReadLine();

            if (string.IsNullOrEmpty(playerInput) || playerInput.ToLower() == "exit")
            {
                break;
            }

            // Add player input to chat history
            _chatHistoryService.AddUserMessage(_sessionId, playerInput);

            // Build prompt with relevant context and chat history
            var prompt = await _memoryService.BuildStoryPromptAsync(playerInput);

            // Send to LLM for response
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function, new KernelArguments(PromptSettings.NormalChatSettings));
            var responseText = result.ToString();

            // Process and display the response
            await ProcessAndDisplayResponse(responseText, playerInput);
        }

        Console.WriteLine("\nThanks for playing!");
    }

    /// <summary>
    /// Creates the initial story scene
    /// </summary>
    private async Task<string> CreateInitialSceneAsync()
    {
        // Try to use the scenario-specific initial prompt if available
        var initialPrompt = _promptService.GetPromptTemplate("initial");

        // Find player character
        var playerCharacter = _storyConfig.Characters.FirstOrDefault(c => c.IsPlayerCharacter);
        var playerDescription = playerCharacter != null
            ? $"{playerCharacter.Name}: {playerCharacter.Personality} - {playerCharacter.Background}"
            : "Player character information not available";

        // Get NPC characters
        var npcCharacters = string.Join("\n", _storyConfig.Characters
            .Where(c => !c.IsPlayerCharacter)
            .Select(c => $"- {c.Name}: {c.Personality} - {c.Background}"));

        // Format the prompt with required variables
        var formattedPrompt = _promptService.FormatPrompt("initial", new Dictionary<string, string>
        {
            ["scenario_description"] = _storyConfig.Title + ": " + _storyConfig.Setting,
            ["setting"] = _storyConfig.Setting,
            ["player_character"] = playerDescription,
            ["npc_characters"] = npcCharacters
        });

        // Create the function and invoke it
        var initialSceneFunction = _kernel.CreateFunctionFromPrompt(formattedPrompt);
        var result = await _kernel.InvokeAsync(initialSceneFunction, new KernelArguments(PromptSettings.NormalChatSettings));

        return result.ToString();
    }

    private async Task ProcessAndDisplayResponse(string responseText, string? playerInput = null)
    {
        try
        {
            // Extract structured information from text response
            var storyInteraction = await _textExtractionService.ExtractStoryInteractionAsync(
                responseText,
                playerInput ?? "Begin the story");

            _ = Task.Run(() => _memoryService.StoreInteractionAsync(storyInteraction.PlayerAction, responseText, storyInteraction));

            // Display the scene description
            if (!string.IsNullOrEmpty(storyInteraction.SceneDescription))
            {
                Console.WriteLine("\n" + storyInteraction.SceneDescription + "\n");
            }
            else
            {
                // If extraction failed, at least show the raw response
                Console.WriteLine("\n" + responseText + "\n");
            }

            // Display character responses
            DisplayCharacterResponses(storyInteraction.CharacterResponses);

            // Add to chat history
            _chatHistoryService.AddAiMessage(_sessionId, storyInteraction);

            // Try to find and display suggested actions
            DisplaySuggestedActions(responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing story response");
            Console.WriteLine("\nThere was an error processing the story response. Please try again.");

            // Fall back to displaying the raw text
            Console.WriteLine("\n" + responseText + "\n");
        }
    }

    private void DisplayCharacterResponses(List<CharacterResponse> characterResponses)
    {
        foreach (var response in characterResponses)
        {
            if (string.IsNullOrEmpty(response.CharacterName) ||
                string.IsNullOrEmpty(response.Dialogue))
            {
                continue;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{response.CharacterName}: ");
            Console.ResetColor();
            Console.WriteLine(response.Dialogue);

            if (!string.IsNullOrEmpty(response.Action))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"({response.Action})");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }

    private void DisplaySuggestedActions(string responseText)
    {
        // Look for suggested actions at the end of the response
        var lines = responseText.Split('\n');
        var suggestedActions = new List<string>();
        bool inSuggestions = false;

        foreach (var line in lines.Reverse())
        {
            var trimmedLine = line.Trim();

            // Look for typical suggestion markers
            if (trimmedLine.Contains("suggest") ||
                trimmedLine.Contains("you could") ||
                trimmedLine.Contains("you might") ||
                trimmedLine.StartsWith("1.") ||
                trimmedLine.StartsWith("•") ||
                trimmedLine.StartsWith("-"))
            {
                inSuggestions = true;

                // Try to handle numbered or bulleted lists
                if (trimmedLine.StartsWith("1.") ||
                    trimmedLine.StartsWith("•") ||
                    trimmedLine.StartsWith("-"))
                {
                    suggestedActions.Add(trimmedLine);
                }
                // If we find an introduction to suggestions, stop here
                else if (trimmedLine.Contains("suggest") ||
                         trimmedLine.Contains("you could") ||
                         trimmedLine.Contains("you might"))
                {
                    break;
                }
            }
            else if (inSuggestions &&
                    (trimmedLine.StartsWith("2.") ||
                     trimmedLine.StartsWith("3.") ||
                     trimmedLine.StartsWith("4.")))
            {
                suggestedActions.Add(trimmedLine);
            }
            else if (inSuggestions && string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Skip empty lines in the suggestion section
                continue;
            }
            else if (inSuggestions)
            {
                // If we've seen suggestions but now hit something else, we're done
                break;
            }
        }

        // If we found suggestions, display them
        if (suggestedActions.Count > 0)
        {
            Console.WriteLine("\nSuggested actions:");
            foreach (var action in suggestedActions.AsEnumerable().Reverse())
            {
                Console.WriteLine(action);
            }
        }
    }
}
