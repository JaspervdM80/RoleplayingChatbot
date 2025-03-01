using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Roleplaying.Chatbot.Engine.Helpers;
using Roleplaying.Chatbot.Engine.Models;
using Roleplaying.Chatbot.Engine.Repositories;
using Roleplaying.Chatbot.Engine.Services;

namespace Roleplaying.Chatbot.Engine;

/// <summary>
/// Main application for the interactive story
/// </summary>
public class InteractiveStoryApp
{
    private readonly StoryService _memoryService;
    private readonly Kernel _kernel;
    private readonly PromptRepository _promptRepository;
    private readonly ILogger<InteractiveStoryApp> _logger;
    private readonly StoryConfig _storyConfig;
    private readonly ChatHistoryService _chatHistoryService;
    private string _sessionId;

    public InteractiveStoryApp(
        StoryService memoryService,
        Kernel kernel,
        PromptRepository promptRepository,
        ChatHistoryService chatHistoryService,
        ILogger<InteractiveStoryApp> logger,
        StoryConfig storyConfig)
    {
        _memoryService = memoryService;
        _kernel = kernel;
        _promptRepository = promptRepository;
        _logger = logger;
        _storyConfig = storyConfig;
        _chatHistoryService = chatHistoryService;
        _sessionId = _chatHistoryService.CreateSession();
    }

    public async Task RunAsync()
    {
        // Keep existing code
        _logger.LogInformation("Starting interactive story: {Title}", _storyConfig.Title);
        // ...

        // Initial story setup - create first scene
        Console.WriteLine("\n--- STORY BEGINS ---\n");

        // If this is first run, create an initial scene
        var initialStoryJson = await CreateInitialSceneAsync();
        await ProcessAndDisplayResponse(initialStoryJson);

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
            var result = await _kernel.InvokeAsync(function);
            var responseJson = result.ToString();

            // Process and display the response
            await ProcessAndDisplayResponse(responseJson, playerInput);
        }

        Console.WriteLine("\nThanks for playing!");
    }

    /// <summary>
    /// Creates the initial story scene
    /// </summary>
    private async Task<string> CreateInitialSceneAsync()
    {
        // Try to use the scenario-specific initial prompt if available
        var initialPrompt =_promptRepository.Get("initial");

        // Find player character
        var playerCharacter = _storyConfig.Characters.FirstOrDefault(c => c.IsPlayerCharacter);
        var playerDescription = playerCharacter != null
            ? $"{playerCharacter.Name}: {playerCharacter.Personality} - {playerCharacter.Background}"
            : "Player character information not available";

        // Get NPC characters
        var npcCharacters = string.Join("\n", _storyConfig.Characters
            .Where(c => !c.IsPlayerCharacter)
            .Select(c => $"- {c.Name}: {c.Personality} - {c.Background}"));

        // Create the function and invoke it
        var initialSceneFunction = _kernel.CreateFunctionFromPrompt(initialPrompt);
        var result = await _kernel.InvokeAsync(initialSceneFunction, new KernelArguments
        {
            ["setting"] = _storyConfig.Setting,
            ["scenario_description"] = _storyConfig.Title + ": " + _storyConfig.Setting,
            ["player_character"] = playerDescription,
            ["npc_characters"] = npcCharacters
        });

        return result.ToString();
    }

    private async Task ProcessAndDisplayResponse(string responseJson, string? playerInput = null)
    {
        try
        {
            await StoreInteractionInMemory(responseJson, playerInput);

            var aiResponse = ResponseHelper.CleanJsonResponse(responseJson);
            var response = JsonSerializer.Deserialize<JsonElement>(aiResponse);

            // Create story interaction for building complete response
            var storyInteraction = new StoryInteraction
            {
                PlayerAction = playerInput ?? "Begin the story",
                CharacterResponses = new List<CharacterResponse>()
            };

            // Process and display each component
            ExtractAndDisplaySceneDescription(response, storyInteraction);
            ExtractAndDisplayCharacterResponses(response, storyInteraction);

            // Add to chat history
            _chatHistoryService.AddAiMessage(_sessionId, storyInteraction);

            DisplayAvailableActions(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing story response");
            Console.WriteLine("\nThere was an error processing the story response. Please try again.");
        }
    }

    private async Task StoreInteractionInMemory(string responseJson, string? playerInput)
    {
        if (playerInput != null)
        {
            await _memoryService.StoreInteractionAsync(playerInput, responseJson);
        }
        else
        {
            await _memoryService.StoreInteractionAsync("Begin the story", responseJson);
        }
    }

    private void ExtractAndDisplaySceneDescription(JsonElement response, StoryInteraction storyInteraction)
    {
        string sceneDescription = "";

        if (response.TryGetProperty("scene_description", out var sceneDescProperty))
        {
            sceneDescription = sceneDescProperty.GetString() ?? "";
        }
        else if (response.TryGetProperty("scene", out var scene) &&
                 scene.TryGetProperty("description", out var sceneDesc))
        {
            sceneDescription = sceneDesc.GetString() ?? "";
        }

        storyInteraction.SceneDescription = sceneDescription;

        if (!string.IsNullOrEmpty(sceneDescription))
        {
            Console.WriteLine("\n" + sceneDescription + "\n");
        }
    }

    private void ExtractAndDisplayCharacterResponses(JsonElement response, StoryInteraction storyInteraction)
    {
        if (!response.TryGetProperty("character_responses", out var characterResponses) ||
            characterResponses.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var charResponse in characterResponses.EnumerateArray())
        {
            var characterResponse = ExtractCharacterResponse(charResponse);
            storyInteraction.CharacterResponses.Add(characterResponse);

            DisplayCharacterResponse(characterResponse);
        }
    }

    private CharacterResponse ExtractCharacterResponse(JsonElement charResponse)
    {
        var characterResponse = new CharacterResponse();

        if (charResponse.TryGetProperty("character_name", out var name) ||
            charResponse.TryGetProperty("character", out name))
        {
            characterResponse.CharacterName = name.GetString() ?? "";
        }

        if (charResponse.TryGetProperty("dialogue", out var dialogueProperty))
        {
            characterResponse.Dialogue = dialogueProperty.GetString() ?? "";
        }

        if (charResponse.TryGetProperty("action", out var actionProperty) ||
            charResponse.TryGetProperty("actions", out actionProperty))
        {
            characterResponse.Action = actionProperty.GetString() ?? "";
        }

        if (charResponse.TryGetProperty("emotion", out var emotionProperty))
        {
            characterResponse.Emotion = emotionProperty.GetString() ?? "";
        }

        return characterResponse;
    }

    private void DisplayCharacterResponse(CharacterResponse characterResponse)
    {
        if (string.IsNullOrEmpty(characterResponse.CharacterName) ||
            string.IsNullOrEmpty(characterResponse.Dialogue))
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{characterResponse.CharacterName}: ");
        Console.ResetColor();
        Console.WriteLine(characterResponse.Dialogue);

        if (!string.IsNullOrEmpty(characterResponse.Action))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"({characterResponse.Action})");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    private void DisplayAvailableActions(JsonElement response)
    {
        JsonElement? availableActions = null;

        if (response.TryGetProperty("available_actions", out var actions))
        {
            availableActions = actions;
        }
        else if (response.TryGetProperty("player_options", out var options) &&
                options.TryGetProperty("suggested_actions", out actions))
        {
            availableActions = actions;
        }

        if (availableActions == null || availableActions.Value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        Console.WriteLine("\nSuggested actions:");
        var i = 1;
        foreach (var action in availableActions.Value.EnumerateArray())
        {
            if (action.ValueKind == JsonValueKind.String)
            {
                Console.WriteLine($"{i}. {action.GetString()}");
                i++;
            }
        }
    }
}
