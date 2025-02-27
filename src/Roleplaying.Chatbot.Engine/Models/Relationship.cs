namespace Roleplaying.Chatbot.Engine.Models;

/// <summary>
/// Defines a relationship between two characters
/// </summary>
public class Relationship
{
    public string Type { get; set; } = string.Empty;
    public float TrustLevel { get; set; } // 0.0 to 1.0
    public float Closeness { get; set; } // 0.0 to 1.0
    public string DynamicState { get; set; } = string.Empty;
}
