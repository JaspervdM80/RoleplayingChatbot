namespace Roleplaying.Chatbot.Engine.Models;

/// <summary>
/// Character definition for the story
/// </summary>
public class Character
{
    public string Name { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty;
    public bool IsPlayerCharacter { get; set; }
    public Dictionary<string, Relationship> Relationships { get; set; } = new();
}
