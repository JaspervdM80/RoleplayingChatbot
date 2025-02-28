using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Roleplaying.Chatbot.Engine.Services;

namespace Roleplaying.Chatbot.Engine.Extensions;

public static class KernelBuilderExtensions
{
    /// <summary>
    /// Adds Ollama text embedding generation to the kernel
    /// </summary>
    public static IKernelBuilder AddOllamaTextEmbeddingGeneration(
        this IKernelBuilder builder,
        string modelId,
        string endpoint = "http://localhost:11434",
        int dimensions = 768,
        bool normalize = false,
        Dictionary<string, object?>? defaultOptions = null)
    {
        builder.Services.AddKeyedSingleton<ITextEmbeddingGenerationService>(
            serviceKey: modelId,
            implementationFactory: (sp, _) =>
                new OllamaTextEmbeddingGeneration(
                    endpoint,
                    modelId,
                    dimensions,
                    normalize,
                    defaultOptions));

        return builder;
    }
}
