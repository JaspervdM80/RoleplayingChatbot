namespace Roleplaying.Chatbot.Engine.Models;

/// <summary>
/// Structured representation of a story interaction
/// </summary>
public class StoryInteraction
{
    // Basic story meta information
    public string Chapter { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string> PresentCharacters { get; set; } = new();

    // The player's action that triggered this interaction
    public string PlayerAction { get; set; } = string.Empty;

    // Scene description
    public string SceneDescription { get; set; } = string.Empty;

    // Character responses
    public List<CharacterResponse> CharacterResponses { get; set; } = new();

    // Narrative progression information
    public string NarrativeProgression { get; set; } = string.Empty;

    // Relationship changes that occurred
    public List<RelationshipChange> RelationshipChanges { get; set; } = new();

    // Any new plot developments
    public List<string> PlotDevelopments { get; set; } = new();
}
