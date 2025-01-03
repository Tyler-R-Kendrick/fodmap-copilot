using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;

HostApplicationBuilder builder = new();

var host = builder.Build();
var apiKey = builder.Configuration["BingApiKey"]
    ?? throw new ArgumentException("BingApiKey is not set in configuration.");
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion("gpt4o");
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernelBuilder.AddBingTextSearch(apiKey);
kernelBuilder.Services.AddSingleton<FodmapResearchPlugin>();
kernelBuilder.Plugins.AddFromType<FodmapResearchPlugin>();
var kernel = kernelBuilder.Build();

// Create an ITextSearch instance using Bing search
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var result = await kernel.InvokePromptAsync("Is broccoli a FODMAP?");
Console.WriteLine(result);
Console.ReadLine(); // Wait for user input before closing
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

await host.RunAsync();

class FodmapResearchPlugin(ITextSearch textSearch)
{
    [KernelFunction("Research FODMAPs")]
    public async Task<TextSearchResult[]> ResearchFodmapsAsync(
        string foodName)
    {
        var prompt = $"Is {foodName} a FODMAP? If so, provide a brief summary of the food related to its FODMAP status.";
        var results = await textSearch.GetTextSearchResultsAsync(prompt, new()
        {
            Top = 4
        });
        List<TextSearchResult> snippets = [];
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        await foreach (var result in results.Results)
        {
            snippets.Add(result);
        }
        return [.. snippets];
    }
}