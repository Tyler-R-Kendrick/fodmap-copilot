using Microsoft.Azure.CognitiveServices.Search.WebSearch;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace App;

public static class Startup
{
    public static IHost CreateHost()
    {
        HostApplicationBuilder builder = new();
        var services = builder.Services;
        Startup.ConfigureConfiguration(builder.Configuration);
        var config = builder.Configuration;
        Startup.ConfigureServices(config, services);
        return builder.Build();
    }

    public static void ConfigureConfiguration(IConfigurationBuilder config)
    {
        config
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(true);
    }

    public static void ConfigureServices(IConfigurationRoot config, IServiceCollection services)
    {
        var bingApiKey = config["BingApiKey"]
            ?? throw new ArgumentException("BingApiKey is not set in configuration.");
        var openAiApiKey = config["OpenAiApiKey"]
            ?? throw new ArgumentException("OpenAiApiKey is not set in configuration.");
        var perplexityApiKey = config["PerplexityApiKey"]
            ?? throw new ArgumentException("PerplexityApiKey is not set in configuration.");
        services.AddSingleton(services => new OpenAIClient(openAiApiKey)
            .AsChatClient("gpt-4o-mini"));
        services.AddSingleton<FodmapResearchPlugin>();
        services.AddSingleton<PerplexitySearchPlugin>();
        services.AddSingleton<SearchReasoningAgent>();
        services.AddTransient(_ => new HttpClient());
        services.AddTransient(_ => new OpenAI.Chat.ChatClient("sonar", new(perplexityApiKey), new()
        {
            Endpoint = new("https://api.perplexity.ai"),
        }));
        services.AddTransient<IWebSearchClient>(_ => new WebSearchClient(
            new ApiKeyServiceClientCredentials(bingApiKey))
            {
                Endpoint = "https://api.bing.microsoft.com"
            });
    }
}
