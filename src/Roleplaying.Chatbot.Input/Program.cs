using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Roleplaying.Chatbot.Engine;
using Roleplaying.Chatbot.Engine.Extensions;
using Roleplaying.Chatbot.Engine.Models;
using Roleplaying.Chatbot.Engine.Repositories;
using Roleplaying.Chatbot.Engine.Services;

// Configure host with DI
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
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

        services.AddSingleton<ScenarioLoaderService>();

        services.AddScenario();

        services.AddSingleton(new PromptRepository().Load());

        // Register story memory service
        services.AddSingleton<StoryService>();

        // Register interactive story app
        services.AddSingleton<InteractiveStoryApp>();
        services.AddSingleton<ChatHistoryService>();

        // Initialize the LangChainPromptService
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LangChainPromptService>>();
            var promptService = new LangChainPromptService(logger);

            // Load templates asynchronously 
            promptService.InitializeTemplatesFromDirectory("./Prompts").GetAwaiter().GetResult();
            return promptService;
        });
    })
    .Build();

// Run the app
await host.Services.GetRequiredService<InteractiveStoryApp>().RunAsync();
