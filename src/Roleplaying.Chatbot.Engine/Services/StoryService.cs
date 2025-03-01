using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Roleplaying.Chatbot.Engine.Abstractions;
using Roleplaying.Chatbot.Engine.Models;
using Roleplaying.Chatbot.Engine.Settings;

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
    private readonly LangChainPromptService _langChainPromptService;
    private readonly ILoggingService _loggingService;

    /// <summary>
    /// Constructor initializes with required dependencies
    /// </summary>
    public StoryService(Kernel kernel, StoryConfig storyConfig, LangChainPromptService langChainPromptService, ILoggingService loggingService)
    {
        _kernel = kernel;
        _embeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _storyConfig = storyConfig;
        _langChainPromptService = langChainPromptService;
        _loggingService = loggingService;

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
            new VectorStoreRecordVectorProperty("ContentEmbedding", typeof(ReadOnlyMemory<float>)) { Dimensions = Constants.EmbeddingDimensions },
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
    /// <summary>
    /// Stores a player action and the resulting story response
    /// </summary>
    public async Task StoreInteractionAsync(string playerAction, string storyResponseText, StoryInteraction? storyInteraction = null)
    {
        try
        {
            // Create a new memory instance
            var memory = new StoryMemory
            {
                Key = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MemoryType = "story_interaction",
                Content = storyResponseText,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Use provided story interaction or create an empty one
            var interaction = storyInteraction ?? new StoryInteraction
            {
                PlayerAction = playerAction,
                SceneDescription = storyResponseText, // Default to full text if not parsed
                CharacterResponses = []
            };

            // Store the structured interaction
            memory.Interaction = interaction;

            // Extract character names involved
            foreach (var characterResponse in interaction.CharacterResponses)
            {
                if (!string.IsNullOrEmpty(characterResponse.CharacterName) &&
                    !memory.CharactersInvolved.Contains(characterResponse.CharacterName))
                {
                    memory.CharactersInvolved.Add(characterResponse.CharacterName);
                }
            }

            // Add location if available
            if (!string.IsNullOrEmpty(interaction.Location))
            {
                memory.LocationsInvolved.Add(interaction.Location);
            }

            // Add plot elements if available
            foreach (var plotElement in interaction.PlotDevelopments)
            {
                if (!string.IsNullOrEmpty(plotElement))
                {
                    memory.PlotElements.Add(plotElement);
                }
            }

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
        }
        catch (Exception ex)
        {
            _ = _loggingService.LogError("StoreInteraction", ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves relevant story memories based on a query
    /// </summary>
    public async Task<List<StoryMemory>> RetrieveRelevantMemoriesAsync(string query, List<string>? characterFilter = null, string? locationFilter = null, int limit = 5)
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
            _ = _loggingService.LogError("RetrieveRelevantMemories", ex);
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
    string templateType = "advanced")
    {
        // Keep existing code to get relevantMemories
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
        var dummyVector = new ReadOnlyMemory<float>(new float[768]);

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

        // Use LangChain for prompt formatting if available       
        var variables = new Dictionary<string, string>
        {
            ["story_setting"] = _storyConfig.Setting,
            ["characters"] = string.Join("\n", charactersData),
            ["player.name"] = playerCharacter?.Name ?? "Player",
            ["player.background"] = playerCharacter?.Background ?? "",
            ["story_context"] = FormatMemoriesForPrompt(relevantMemories),
            ["recent_history"] = recentHistory,
            ["current_location"] = currentLocation,
            ["player_action"] = playerAction
        };

        return _langChainPromptService.FormatPrompt(templateType, variables);
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
            // Build character responses string
            var characterResponsesText = string.Join("\n", memory.Interaction.CharacterResponses.Select(cr =>
                $"{cr.CharacterName}: {cr.Dialogue} {(string.IsNullOrEmpty(cr.Action) ? "" : $"({cr.Action})")}"));

            // Format the summarization prompt with our variables
            var formattedPrompt = _langChainPromptService.FormatPrompt("summarize", new Dictionary<string, string>
            {
                ["playerAction"] = memory.Interaction.PlayerAction,
                ["sceneDescription"] = memory.Interaction.SceneDescription,
                ["characterResponses"] = characterResponsesText
            });

            // Execute the prompt
            var summarizationFunction = _kernel.CreateFunctionFromPrompt(formattedPrompt);
            var result = await _kernel.InvokeAsync(
                    function: summarizationFunction,
                    arguments: new KernelArguments(PromptSettings.SummrarizeChatSettings));

            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            _ = _loggingService.LogError("GenerateSummary", ex);

            // Fallback summary if LLM fails
            return $"Interaction involving {string.Join(", ", memory.CharactersInvolved)} at {memory.Interaction.Location}.";
        }
    }
}
