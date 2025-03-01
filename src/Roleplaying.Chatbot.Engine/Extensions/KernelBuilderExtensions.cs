using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    public static IServiceCollection AddScenario(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var scenarioLoader = sp.GetRequiredService<ScenarioLoaderService>();
            var logger = sp.GetRequiredService<ILogger<ScenarioLoaderService>>();

            try
            {
                // Look for scenario files in the current directory or a scenarios subfolder
                string[] potentialPaths = {
                    "./Scenarios/sleepover_scenario.json",
                    "scenarios/sleepover_scenario.json",
                    "../sleepover_scenario.json"
                };

                string scenarioPath = potentialPaths.FirstOrDefault(File.Exists)
                    ?? throw new FileNotFoundException("Could not find scenario file");

                logger.LogInformation("Loading scenario from {Path}", scenarioPath);
                var scenario = scenarioLoader.LoadFromFile(scenarioPath);

                // Convert to StoryConfig for compatibility with existing code
                return scenario.ToStoryConfig();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load scenario, falling back to default");
                throw;
            }
        });

        return services;
    }

    public static IServiceCollection AddPromptService(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LangChainPromptService>>();
            var promptService = new LangChainPromptService(logger);

            // Load templates asynchronously 
            promptService.InitializeTemplatesFromDirectory("./Prompts").GetAwaiter().GetResult();
            return promptService;
        });

        return services;
    }

    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
        // Configure kernel
        var kernelBuilder = Kernel.CreateBuilder();

        // Configure your LLM service here
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "phi3", // Use your preferred model
            openAIClient: new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential("no-key"), new OpenAI.OpenAIClientOptions() { Endpoint = new Uri("http://localhost:11434/v1") })
            );

        // Configure embedding service - use an appropriate embedding model
        kernelBuilder.AddOllamaTextEmbeddingGeneration(
            modelId: "nomic-embed-text",
            dimensions: 768
            );

        // Configure Qdrant
        kernelBuilder.AddQdrantVectorStore(
            host: "localhost",
            port: 6334,
            https: false
            );

        var kernel = kernelBuilder.Build();

        // Add kernel to DI
        services.AddSingleton(kernel);

        return services;
    }
}
