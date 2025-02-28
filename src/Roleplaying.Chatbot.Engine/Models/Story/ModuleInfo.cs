﻿using System.Text.Json.Serialization;

namespace Roleplaying.Chatbot.Engine.Models.Story;

public class ModuleInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
