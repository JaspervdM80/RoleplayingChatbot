namespace Roleplaying.Chatbot.Engine.Models;

public class CharacterResponse
{
    public string CharacterName { get; set; } = string.Empty;
    public string Dialogue { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Emotion { get; set; } = string.Empty;
    public string InternalThoughts { get; set; } = string.Empty;
}
