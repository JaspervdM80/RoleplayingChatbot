using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Roleplaying.Chatbot.Engine;
using Roleplaying.Chatbot.Engine.Extensions;
using Roleplaying.Chatbot.Engine.Models;
using Roleplaying.Chatbot.Engine.Repositories;
using Roleplaying.Chatbot.Engine.Services;

// Configure host with DI
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(async (context, services) =>
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

        // Create and register story config
        services.AddSingleton(CreateStoryConfig());

        // Register story memory service
        services.AddSingleton<StoryService>();

        // Register interactive story app
        services.AddSingleton<InteractiveStoryApp>();

        var prompts = new PromptRepository();
        await prompts.LoadAsync();

        services.AddSingleton(prompts);
    })
    .Build();

// Run the app
await host.Services.GetRequiredService<InteractiveStoryApp>().RunAsync();


// Create a sample story config
static StoryConfig CreateStoryConfig()
{
    return new StoryConfig
    {
        Title = "Mystery at Blackwood Manor",
        Setting = "A mysterious old mansion on the outskirts of a small town in the 1920s",
        Genre = "Mystery",
        Characters = new List<Character>
            {
                new Character
                {
                    Name = "Detective Morgan",
                    Personality = "Sharp, observant, and slightly cynical. Has a dry sense of humor and a keen eye for details others miss.",
                    Background = "Former police detective who now works privately, known for solving cases others consider impossible.",
                    Motivation = "To uncover the truth, no matter the cost",
                    IsPlayerCharacter = true
                },
                new Character
                {
                    Name = "Lady Eleanor Blackwood",
                    Personality = "Elegant, enigmatic, and guarded. Speaks carefully and reveals little about herself.",
                    Background = "The last surviving member of the Blackwood family, who inherited the manor after the mysterious deaths of her parents.",
                    Motivation = "To protect family secrets and maintain her social standing"
                },
                new Character
                {
                    Name = "Dr. James Harrison",
                    Personality = "Intellectual, articulate, and slightly arrogant. Confident in his knowledge and professional status.",
                    Background = "The family physician who has served the Blackwoods for decades and knows all their medical history.",
                    Motivation = "To preserve his reputation and conceal his mistakes"
                },
                new Character
                {
                    Name = "Mrs. Reynolds",
                    Personality = "Stern, loyal, and traditional. Speaks directly and values honesty.",
                    Background = "The head housekeeper who has worked at the manor for over 30 years and is fiercely protective of the Blackwood family.",
                    Motivation = "To protect the Blackwood family and maintain order in the household"
                }
            }
    };
}
