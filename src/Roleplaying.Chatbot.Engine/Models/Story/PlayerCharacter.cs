using System.Text.Json.Serialization;

namespace Roleplaying.Chatbot.Engine.Models.Story;

public class PlayerCharacter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("personalityTraits")]
    public Dictionary<string, string> PersonalityTraits { get; set; } = [];
}
