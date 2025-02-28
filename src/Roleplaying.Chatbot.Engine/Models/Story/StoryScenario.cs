using System.Text.Json.Serialization;

namespace Roleplaying.Chatbot.Engine.Models.Story;

/// <summary>
/// Represents a complete story scenario with all required elements
/// </summary>
public class StoryScenario
{
    [JsonPropertyName("moduleInfo")]
    public ModuleInfo ModuleInfo { get; set; } = new();

    [JsonPropertyName("playerCharacter")]
    public PlayerCharacter PlayerCharacter { get; set; } = new();

    [JsonPropertyName("npcs")]
    public List<NpcCharacter> Npcs { get; set; } = new();

    [JsonPropertyName("storyBackground")]
    public List<BackgroundElement> StoryBackground { get; set; } = new();

    // Helper method to convert to StoryConfig for compatibility with existing code
    public StoryConfig ToStoryConfig()
    {
        var config = new StoryConfig
        {
            Title = ModuleInfo.Name,
            Setting = string.Join(" ", StoryBackground.Select(b => b.Summary)),
            Genre = ModuleInfo.Description.Split('.')[0], // Just use the first sentence as genre
            Characters = []
        };

        // Add player character
        config.Characters.Add(new Character
        {
            Name = PlayerCharacter.Name,
            Personality = PlayerCharacter.PersonalityTraits.GetValueOrDefault("personality", ""),
            Background = PlayerCharacter.Description,
            Motivation = "To engage with the story",
            IsPlayerCharacter = true
        });

        // Add NPCs
        foreach (var npc in Npcs)
        {
            config.Characters.Add(new Character
            {
                Name = npc.Name,
                Personality = npc.PersonalityTraits.GetValueOrDefault("personality", ""),
                Background = npc.BackStory,
                Motivation = npc.PersonalityTraits.GetValueOrDefault("role", ""),
                IsPlayerCharacter = false
            });
        }

        return config;
    }
}
