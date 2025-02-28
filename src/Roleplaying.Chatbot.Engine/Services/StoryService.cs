using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Roleplaying.Chatbot.Engine.Models;
using Roleplaying.Chatbot.Engine.Repositories;

namespace Roleplaying.Chatbot.Engine.Services;


/// <summary>
/// Service for managing story memories using Semantic Kernel and Qdrant
/// </summary>
public class StoryService
{
    private readonly Kernel _kernel;
    private readonly IVectorStoreRecordCollection<ulong, StoryMemory> _collection;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly StoryConfig _storyConfig;
    private readonly PromptRepository _promptRepository;
    private readonly ILogger<StoryService> _logger;

    // Vector dimension count from the embedding model
    private const int EmbeddingDimensions = 768; // Change based on your model

    /// <summary>
    /// Constructor initializes with required dependencies
    /// </summary>
    public StoryService(
        Kernel kernel,
        StoryConfig storyConfig,
        PromptRepository promptRepository,
        ILogger<StoryService> logger)
    {
        _kernel = kernel;
        _embeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _storyConfig = storyConfig;
        _promptRepository = promptRepository;
        _logger = logger;

        // Define vector store schema
        var memoryDefinition = new VectorStoreRecordDefinition
        {
            Properties = [
                new VectorStoreRecordKeyProperty("Key", typeof(ulong)),
                new VectorStoreRecordDataProperty("MemoryType", typeof(string)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("Content", typeof(string)) { IsFullTextSearchable = true },
                new VectorStoreRecordDataProperty("Timestamp", typeof(long)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("CharactersInvolved", typeof(List<string>)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("LocationsInvolved", typeof(List<string>)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("PlotElements", typeof(List<string>)) { IsFilterable = true },
                new VectorStoreRecordVectorProperty("ContentEmbedding", typeof(ReadOnlyMemory<float>)) { Dimensions = EmbeddingDimensions },
                new VectorStoreRecordDataProperty("EmotionalValence", typeof(float)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("Importance", typeof(float)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("Summary", typeof(string)) { IsFullTextSearchable = true },
            ]
        };

        var vectorStore = (kernel.GetRequiredService<IVectorStore>() as QdrantVectorStore)!;

        // Get collection with the defined schema
        _collection = vectorStore.GetCollection<ulong, StoryMemory>("StoryMemories", memoryDefinition);

        // Initialize collection
        _collection.CreateCollectionIfNotExistsAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stores a player action and the resulting story response
    /// </summary>
    public async Task<ulong> StoreInteractionAsync(string playerAction, string storyResponseJson)
    {
        try
        {
            // Parse the story response JSON
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Attempt to parse the JSON structure to extract structured data
            JsonElement jsonResponse = JsonSerializer.Deserialize<JsonElement>(storyResponseJson);

            // Create a new memory instance
            var memory = new StoryMemory
            {
                Key = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MemoryType = "story_interaction",
                Content = storyResponseJson,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Extract core information based on the JSON structure
            var interaction = new StoryInteraction
            {
                PlayerAction = playerAction
            };

            // Extract relevant information from the JSON response
            // Note: This uses a try/catch approach to handle different JSON structures
            try
            {
                // Try to extract scene description
                if (jsonResponse.TryGetProperty("scene_description", out var sceneDescription))
                {
                    interaction.SceneDescription = sceneDescription.GetString() ?? "";
                }
                else if (jsonResponse.TryGetProperty("scene", out var scene) &&
                         scene.TryGetProperty("description", out var sceneDesc))
                {
                    interaction.SceneDescription = sceneDesc.GetString() ?? "";
                }
                else if (jsonResponse.TryGetProperty("narrative_response", out var narrativeResponse) &&
                         narrativeResponse.TryGetProperty("scene_description", out var narrativeSceneDesc))
                {
                    interaction.SceneDescription = narrativeSceneDesc.GetString() ?? "";
                }

                // Try to extract character responses
                if (jsonResponse.TryGetProperty("character_responses", out var characterResponses) &&
                    characterResponses.ValueKind == JsonValueKind.Array)
                {
                    foreach (var response in characterResponses.EnumerateArray())
                    {
                        var characterResponse = new CharacterResponse();

                        if (response.TryGetProperty("character_name", out var characterName) ||
                            response.TryGetProperty("character", out characterName))
                        {
                            characterResponse.CharacterName = characterName.GetString() ?? "";
                        }

                        if (response.TryGetProperty("dialogue", out var dialogue))
                        {
                            characterResponse.Dialogue = dialogue.GetString() ?? "";
                        }

                        if (response.TryGetProperty("action", out var action) ||
                            response.TryGetProperty("actions", out action))
                        {
                            characterResponse.Action = action.GetString() ?? "";
                        }

                        if (response.TryGetProperty("emotion", out var emotion) ||
                            response.TryGetProperty("emotional_state", out emotion))
                        {
                            characterResponse.Emotion = emotion.GetString() ?? "";
                        }

                        if (response.TryGetProperty("internal_thoughts", out var internalThoughts))
                        {
                            characterResponse.InternalThoughts = internalThoughts.GetString() ?? "";
                        }

                        interaction.CharacterResponses.Add(characterResponse);

                        // Add to characters involved list
                        if (!string.IsNullOrEmpty(characterResponse.CharacterName) &&
                            !memory.CharactersInvolved.Contains(characterResponse.CharacterName))
                        {
                            memory.CharactersInvolved.Add(characterResponse.CharacterName);
                        }
                    }
                }

                // Try to extract location
                if (jsonResponse.TryGetProperty("current_location", out var location))
                {
                    interaction.Location = location.GetString() ?? "";
                    if (!string.IsNullOrEmpty(interaction.Location))
                    {
                        memory.LocationsInvolved.Add(interaction.Location);
                    }
                }

                // Try to extract narrative progression
                if (jsonResponse.TryGetProperty("narrative_progression", out var narrativeProgression))
                {
                    interaction.NarrativeProgression = narrativeProgression.GetString() ?? "";
                }

                // Try to extract relationship changes
                if (jsonResponse.TryGetProperty("relationship_changes", out var relationshipChanges) &&
                    relationshipChanges.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in relationshipChanges.EnumerateArray())
                    {
                        var relationshipChange = new RelationshipChange();

                        // Fix: Initialize between variable before use
                        JsonElement between = default;

                        if (change.TryGetProperty("character1", out var character1) ||
                            (change.TryGetProperty("between", out between) &&
                             between.ValueKind == JsonValueKind.Array &&
                             between.GetArrayLength() > 0))
                        {
                            relationshipChange.Character1 = character1.ValueKind != JsonValueKind.Undefined ?
                                character1.GetString() ?? "" :
                                between[0].GetString() ?? "";
                        }

                        if (change.TryGetProperty("character2", out var character2) ||
                            (between.ValueKind != JsonValueKind.Undefined &&
                             between.ValueKind == JsonValueKind.Array &&
                             between.GetArrayLength() > 1))
                        {
                            relationshipChange.Character2 = character2.ValueKind != JsonValueKind.Undefined ?
                                character2.GetString() ?? "" :
                                between[1].GetString() ?? "";
                        }

                        if (change.TryGetProperty("change", out var changeDescription))
                        {
                            relationshipChange.Change = changeDescription.GetString() ?? "";
                        }

                        if (change.TryGetProperty("reason", out var reason))
                        {
                            relationshipChange.Reason = reason.GetString() ?? "";
                        }

                        interaction.RelationshipChanges.Add(relationshipChange);
                    }
                }

                // Extract plot developments
                if (jsonResponse.TryGetProperty("plot_developments", out var plotDevelopments) &&
                    plotDevelopments.ValueKind == JsonValueKind.Array)
                {
                    foreach (var development in plotDevelopments.EnumerateArray())
                    {
                        if (development.ValueKind == JsonValueKind.String)
                        {
                            var plotElement = development.GetString() ?? "";
                            if (!string.IsNullOrEmpty(plotElement))
                            {
                                interaction.PlotDevelopments.Add(plotElement);
                                memory.PlotElements.Add(plotElement);
                            }
                        }
                    }
                }
                else if (jsonResponse.TryGetProperty("story_tracking", out var storyTracking) &&
                         storyTracking.TryGetProperty("plot_developments", out plotDevelopments) &&
                         plotDevelopments.ValueKind == JsonValueKind.Array)
                {
                    foreach (var development in plotDevelopments.EnumerateArray())
                    {
                        if (development.ValueKind == JsonValueKind.String)
                        {
                            var plotElement = development.GetString() ?? "";
                            if (!string.IsNullOrEmpty(plotElement))
                            {
                                interaction.PlotDevelopments.Add(plotElement);
                                memory.PlotElements.Add(plotElement);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse some JSON components, continuing with partial data");
            }

            // Store the structured interaction
            memory.Interaction = interaction;

            // Generate importance score based on character emotions and plot developments
            memory.Importance = CalculateImportance(memory);

            // Create a summary using the LLM for quick retrieval
            memory.Summary = await GenerateSummaryAsync(memory);

            // Generate embedding for the full content + summary
            var contentToEmbed = $"{memory.Summary}\n{interaction.SceneDescription}\n" +
                string.Join("\n", interaction.CharacterResponses.Select(cr =>
                    $"{cr.CharacterName}: {cr.Dialogue} {cr.Action}").ToList());

            memory.ContentEmbedding = await _embeddingService.GenerateEmbeddingAsync(contentToEmbed);

            // Store in vector database
            await _collection.UpsertAsync(memory);

            _logger.LogInformation("Stored memory with key {Key}", memory.Key);

            return memory.Key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store story interaction");
            throw;
        }
    }

    /// <summary>
    /// Retrieves relevant story memories based on a query
    /// </summary>
    public async Task<List<StoryMemory>> RetrieveRelevantMemoriesAsync(
        string query,
        List<string>? characterFilter = null,
        string? locationFilter = null,
        int limit = 5)
    {
        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            // Build filter expression
            VectorSearchFilter? filters = null;

            if ((characterFilter != null && characterFilter.Count > 0) || !string.IsNullOrEmpty(locationFilter))
            {
                filters = new VectorSearchFilter();

                // Add character filter if provided
                if (characterFilter != null && characterFilter.Count > 0)
                {
                    filters.EqualTo(nameof(StoryMemory.CharactersInvolved), characterFilter);
                }

                // Add location filter if provided
                if (!string.IsNullOrEmpty(locationFilter))
                {
                    filters.EqualTo(nameof(StoryMemory.LocationsInvolved), locationFilter);
                }
            }

            // Perform the vector search
            var searchOptions = new VectorSearchOptions
            {
                IncludeVectors = false,
                Top = limit,
                Filter = filters
            };

            // Use the VectorizedSearchAsync method and access results properly
            var searchResponse = await _collection.VectorizedSearchAsync(queryEmbedding, searchOptions);
            var searchResults = await searchResponse.Results.ToListAsync();

            // Extract and return the records from the results
            return searchResults.Select(result => result.Record).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relevant memories");
            throw;
        }
    }

    /// <summary>
    /// Formats retrieved memories for inclusion in the prompt
    /// </summary>
    public static string FormatMemoriesForPrompt(List<StoryMemory> memories)
    {
        var builder = new System.Text.StringBuilder();

        foreach (var memory in memories.OrderBy(m => m.Timestamp))
        {
            builder.AppendLine($"MEMORY ID: {memory.Key}");
            builder.AppendLine($"RELEVANCE: High");
            builder.AppendLine($"EVENT: {memory.Summary}");
            builder.AppendLine($"CHARACTERS: {string.Join(", ", memory.CharactersInvolved)}");
            builder.AppendLine($"LOCATION: {memory.Interaction.Location}");

            // Add emotional impact if available
            var emotions = memory.Interaction.CharacterResponses
                .Where(cr => !string.IsNullOrEmpty(cr.Emotion))
                .Select(cr => $"{cr.CharacterName}: {cr.Emotion}")
                .ToList();

            if (emotions.Count > 0)
            {
                builder.AppendLine($"EMOTIONAL IMPACT: {string.Join(", ", emotions)}");
            }

            // Add plot developments
            if (memory.Interaction.PlotDevelopments.Count > 0)
            {
                builder.AppendLine($"NARRATIVE SIGNIFICANCE: {string.Join(", ", memory.Interaction.PlotDevelopments)}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds a complete prompt for the story continuation
    /// </summary>
    public async Task<string> BuildStoryPromptAsync(
        string playerAction,
        string templateType = "basic")
    {
        // Get relevant memories
        var relevantMemories = await RetrieveRelevantMemoriesAsync(playerAction);

        // Get character data from story config
        var charactersData = _storyConfig.Characters
            .Select(c => $"* {c.Name}: {c.Personality}")
            .ToList();

        // Find the player character
        var playerCharacter = _storyConfig.Characters.FirstOrDefault(c => c.IsPlayerCharacter);

        // Get recent history (last 3 interactions)
        // Search for all memories, sort by timestamp, and take the 3 most recent
        var memorySearchOptions = new VectorSearchOptions
        {
            Top = 100, // Get enough records to sort through
            IncludeVectors = false
        };

        // Use a dummy vector for search when we just want to filter/sort
        var dummyVector = new ReadOnlyMemory<float>(new float[384]);

        // Get all memories
        var allMemoriesResponse = await _collection.VectorizedSearchAsync(dummyVector, memorySearchOptions);

        // Sort and take most recent 3 memories
        var recentMemories = await allMemoriesResponse.Results
            .Select(r => r.Record)
            .OrderByDescending(m => m.Timestamp)
            .Take(3)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // Format recent history
        var recentHistory = string.Join("\n\n", recentMemories.Select(m =>
            $"[Player] {m.Interaction.PlayerAction}\n" +
            string.Join("\n", m.Interaction.CharacterResponses.Select(cr =>
                $"[{cr.CharacterName}] {cr.Dialogue} {(string.IsNullOrEmpty(cr.Action) ? "" : $"({cr.Action})")}"))));

        // Get current location
        var currentLocation = recentMemories.LastOrDefault()?.Interaction.Location ?? _storyConfig.Setting;

        // Choose the appropriate template
        string template;
        switch (templateType.ToLower())
        {
            case "advanced":
                template = GetAdvancedTemplate();
                break;
            case "vector":
                template = GetVectorTemplate();
                break;
            default:
                template = GetBasicTemplate();
                break;
        }

        // Replace placeholders in the template
        var prompt = template
            .Replace("{{story_setting}}", _storyConfig.Setting)
            .Replace("{{#each characters}}", "")
            .Replace("{{/each}}", "")
            .Replace("* {{name}}: {{personality}}", string.Join("\n", charactersData))
            .Replace("{{player.name}}", playerCharacter?.Name ?? "Player")
            .Replace("{{player.background}}", playerCharacter?.Background ?? "")
            .Replace("{{story_context}}", FormatMemoriesForPrompt(relevantMemories))
            .Replace("{{recent_history}}", recentHistory)
            .Replace("{{current_location}}", currentLocation)
            .Replace("{{player_action}}", playerAction);

        return prompt;
    }

    /// <summary>
    /// Calculate importance score based on story content
    /// </summary>
    private static float CalculateImportance(StoryMemory memory)
    {
        var score = 0.5f; // Default medium importance

        // More important if multiple characters are involved
        score += Math.Min(memory.CharactersInvolved.Count * 0.1f, 0.3f);

        // More important if there are plot developments
        score += Math.Min(memory.PlotElements.Count * 0.15f, 0.3f);

        // More important if there are relationship changes
        score += Math.Min(memory.Interaction.RelationshipChanges.Count * 0.2f, 0.4f);

        // Cap between 0 and 1
        return Math.Clamp(score, 0.0f, 1.0f);
    }

    /// <summary>
    /// Generate a summary of the memory using LLM
    /// </summary>
    private async Task<string> GenerateSummaryAsync(StoryMemory memory)
    {
        try
        {
            var summarizationPrompt = _promptRepository.Get("summarize");

            // Execute the prompt
            var summarizationFunction = _kernel.CreateFunctionFromPrompt(summarizationPrompt);

            // Build character responses string
            var characterResponsesText = string.Join("\n", memory.Interaction.CharacterResponses.Select(cr =>
                $"{cr.CharacterName}: {cr.Dialogue} {(string.IsNullOrEmpty(cr.Action) ? "" : $"({cr.Action})")}"));

            var result = await _kernel.InvokeAsync(summarizationFunction, new KernelArguments
            {
                ["playerAction"] = memory.Interaction.PlayerAction,
                ["sceneDescription"] = memory.Interaction.SceneDescription,
                ["characterResponses"] = characterResponsesText
            });

            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate memory summary");

            // Fallback summary if LLM fails
            return $"Interaction involving {string.Join(", ", memory.CharactersInvolved)} at {memory.Interaction.Location}.";
        }
    }

    // Template getters
    private string GetBasicTemplate()
    {
        return _promptRepository.Get("default");
    }

    private string GetAdvancedTemplate()
    {
        // Return advanced template (abbreviated for brevity)
        return @"You are an advanced narrative engine for an interactive story experience. Your task is to generate dynamic, character-driven responses based on relationships, history, and narrative context.

## WORLD INFORMATION
Setting: {{story_setting}}
..."; // Full template would be included here
    }

    private string GetVectorTemplate()
    {
        // Return vector-aware template (abbreviated for brevity)
        return @"You are an AI narrator managing an interactive story with vector-based memory retrieval. Your responses should be faithful to the provided context, especially the relevant memories retrieved from your vector database.

## STORY CONFIGURATION
..."; // Full template would be included here
    }
}
