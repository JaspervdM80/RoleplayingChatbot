using System.Text.Json.Serialization;

namespace Roleplaying.Chatbot.Engine.Models.Story;

public class NpcCharacter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("backStory")]
    public string BackStory { get; set; } = string.Empty;

    [JsonPropertyName("personalityTraits")]
    public Dictionary<string, string> PersonalityTraits { get; set; } = [];

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
}
