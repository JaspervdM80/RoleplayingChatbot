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

    public InteractiveStoryApp(
        StoryService memoryService,
        Kernel kernel,
        PromptRepository promptRepository,
        ILogger<InteractiveStoryApp> logger,
        StoryConfig storyConfig)
    {
        _memoryService = memoryService;
        _kernel = kernel;
        _promptRepository = promptRepository;
        _logger = logger;
        _storyConfig = storyConfig;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting interactive story: {Title}", _storyConfig.Title);
        Console.WriteLine($"\nWelcome to {_storyConfig.Title}!");
        Console.WriteLine($"Setting: {_storyConfig.Setting}");
        Console.WriteLine("\nCharacters:");
        foreach (var character in _storyConfig.Characters)
        {
            Console.WriteLine($"- {character.Name}: {character.Personality}");
        }

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

            // Build prompt with relevant context
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

    /// <summary>
    /// Process and display the story response
    /// </summary>
    private async Task ProcessAndDisplayResponse(string responseJson, string? playerInput = null)
    {
        try
        {
            // Store in the memory service
            if (playerInput != null)
            {
                await _memoryService.StoreInteractionAsync(playerInput, responseJson);
            }
            else
            {
                // For initial scene, use a placeholder player action
                await _memoryService.StoreInteractionAsync("Begin the story", responseJson);
            }

            var aiResponse = ResponseHelper.CleanJsonResponse(responseJson);

            // Parse the JSON
            var response = JsonSerializer.Deserialize<JsonElement>(aiResponse);

            // Extract and display scene description
            var sceneDescription = "";
            if (response.TryGetProperty("scene_description", out var sceneDescProperty))
            {
                sceneDescription = sceneDescProperty.GetString() ?? "";
            }
            else if (response.TryGetProperty("scene", out var scene) &&
                     scene.TryGetProperty("description", out var sceneDesc))
            {
                sceneDescription = sceneDesc.GetString() ?? "";
            }

            if (!string.IsNullOrEmpty(sceneDescription))
            {
                Console.WriteLine("\n" + sceneDescription + "\n");
            }

            // Extract and display character responses
            if (response.TryGetProperty("character_responses", out var characterResponses) &&
                characterResponses.ValueKind == JsonValueKind.Array)
            {
                foreach (var charResponse in characterResponses.EnumerateArray())
                {
                    var characterName = "";
                    var dialogue = "";
                    var action = "";

                    if (charResponse.TryGetProperty("character_name", out var name) ||
                        charResponse.TryGetProperty("character", out name))
                    {
                        characterName = name.GetString() ?? "";
                    }

                    if (charResponse.TryGetProperty("dialogue", out var dialogueProperty))
                    {
                        dialogue = dialogueProperty.GetString() ?? "";
                    }

                    if (charResponse.TryGetProperty("action", out var actionProperty) ||
                        charResponse.TryGetProperty("actions", out actionProperty))
                    {
                        action = actionProperty.GetString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(dialogue))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"{characterName}: ");
                        Console.ResetColor();
                        Console.WriteLine(dialogue);

                        if (!string.IsNullOrEmpty(action))
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"({action})");
                            Console.ResetColor();
                        }

                        Console.WriteLine();
                    }
                }
            }

            // Display available actions if present
            if (response.TryGetProperty("available_actions", out var availableActions) &&
                availableActions.ValueKind == JsonValueKind.Array)
            {
                Console.WriteLine("\nSuggested actions:");
                var i = 1;
                foreach (var action in availableActions.EnumerateArray())
                {
                    if (action.ValueKind == JsonValueKind.String)
                    {
                        Console.WriteLine($"{i}. {action.GetString()}");
                        i++;
                    }
                }
            }
            else if (response.TryGetProperty("player_options", out var playerOptions) &&
                     playerOptions.TryGetProperty("suggested_actions", out availableActions) &&
                     availableActions.ValueKind == JsonValueKind.Array)
            {
                Console.WriteLine("\nSuggested actions:");
                var i = 1;
                foreach (var action in availableActions.EnumerateArray())
                {
                    if (action.ValueKind == JsonValueKind.String)
                    {
                        Console.WriteLine($"{i}. {action.GetString()}");
                        i++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing story response");
            Console.WriteLine("\nThere was an error processing the story response. Please try again.");
        }
    }
}
