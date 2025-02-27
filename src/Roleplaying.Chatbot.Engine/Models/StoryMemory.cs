namespace Roleplaying.Chatbot.Engine.Models;

/// <summary>
/// Main class for storing story memory in the vector database
/// </summary>
public class StoryMemory
{
    // Required key for vector store
    public ulong Key { get; set; }

    // Memory type to help with filtering
    public string MemoryType { get; set; } = "story_interaction";

    // The raw interaction content
    public string Content { get; set; } = string.Empty;

    // Timestamp for temporal ordering
    public long Timestamp { get; set; }

    // Structured data parsed from JSON responses
    public StoryInteraction Interaction { get; set; } = new();

    // Character names mentioned in this memory for filtering
    public List<string> CharactersInvolved { get; set; } = new();

    // Locations mentioned in this memory for filtering
    public List<string> LocationsInvolved { get; set; } = new();

    // Plot elements for semantic understanding
    public List<string> PlotElements { get; set; } = new();

    // Embedded vector representation of the content
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }

    // Emotional valence of this memory (-1.0 to 1.0)
    public float EmotionalValence { get; set; }

    // Importance score for this memory (0.0 to 1.0)
    public float Importance { get; set; }

    // Summary for quick reference without loading full content
    public string Summary { get; set; } = string.Empty;
}