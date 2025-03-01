using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Roleplaying.Chatbot.Engine;
using Roleplaying.Chatbot.Engine.Extensions;
using Roleplaying.Chatbot.Engine.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSemanticKernel();
        services.AddSingleton<ScenarioLoaderService>();

        services.AddScenario();

        // Register story memory service
        services.AddSingleton<TextExtractionService>();
        services.AddSingleton<StoryService>();
        services.AddSingleton<InteractiveStoryApp>();
        services.AddSingleton<ChatHistoryService>();

        services.AddPromptService();
    })
    .Build();

await host.Services.GetRequiredService<InteractiveStoryApp>().RunAsync();
