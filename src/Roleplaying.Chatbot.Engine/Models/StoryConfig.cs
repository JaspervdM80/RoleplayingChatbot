namespace Roleplaying.Chatbot.Engine.Models;

public class StoryConfig
{
    public string Title { get; set; } = string.Empty;
    public string Setting { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<Character> Characters { get; set; } = new();
}
