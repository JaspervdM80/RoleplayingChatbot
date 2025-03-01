using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Roleplaying.Chatbot.Engine.Settings;

public static class PromptSettings
{
    public static OpenAIPromptExecutionSettings NormalChatSettings => new()
    {
        Temperature = 0.6f,
        MaxTokens = 1000,       
        FrequencyPenalty = 0.4f,
        PresencePenalty = 0.2f,
    };

    public static OpenAIPromptExecutionSettings SummrarizeChatSettings => new()
    {
        Temperature = 0.2f,
        MaxTokens = 350,
        FrequencyPenalty = 0.1f,
        PresencePenalty = 0.1f
    };
}
