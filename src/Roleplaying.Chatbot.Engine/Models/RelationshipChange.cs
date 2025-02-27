namespace Roleplaying.Chatbot.Engine.Models;

/// <summary>
/// Tracks changes in character relationships
/// </summary>
public class RelationshipChange
{
    public string Character1 { get; set; } = string.Empty;
    public string Character2 { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public float ChangeValue { get; set; } // Numerical representation of change
}
