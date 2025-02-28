using System.Text.Json.Serialization;

namespace Roleplaying.Chatbot.Engine.Models.Story;

public class BackgroundElement
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public Dictionary<string, string> Context { get; set; } = [];
}
