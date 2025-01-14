using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using OpenAI.Chat;

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

class FodmapResearchPlugin(IChatClient chatClient)
{
    [Description("The level at which a food sensitivity/intolerance is experienced.")]
    public enum IntoleranceLevel
    {
        [Description("High sensitivity")]
        High,
        [Description("Moderate sensitivity")]
        Moderate,
        [Description("Low sensitivity")]
        Low,
        [Description("No detectable sensitivity")]
        None
    }

    [Flags, Description("The categories of food sensitivities for classification.")]
    public enum SensitivityCategory : long
    {
        [Description("No sensitivity")]
        None = 0,
        [Description("Fructans")]
        Fructans,
        [Description("Ogliosaccharides")]
        Ogliosaccharides,
        [Description("Disaccharides")]
        Disaccharides,
        [Description("Monosaccharides")]
        Monosaccharides,
        [Description("Polyols")]
        Polyols,
        [Description("Dairy")]
        Dairy,
        [Description("Gluten")]
        Gluten,
    }

    [Description("The level of sensitivity to a particular category of food.")]
    public record SensitivityLevel(
        [Description("The category of food sensitivity.")]
        SensitivityCategory Sensitivity,
        [Description("The level of intolerance to the food category.")]
        IntoleranceLevel IntoleranceLevel);
    
    [Description("The food name and its associated sensitivity levels.")]
    public record FoodSensitivity(
        [Description("The name of the food being researched.")]
        string FoodName,
        [Description("The sensitivity levels for the food.")]
        SensitivityLevel[] SensitivityLevels);

    [Description("Research Food sensitivities for a given food name.")]
    public async Task<FoodSensitivity> ResearchFoodSensitivityAsync(
        [Description("The name of the food being researched.")] string foodName)
    {
        var sensitivityCategoryNames = Enum.GetNames<SensitivityCategory>();
        var intoleranceLevelNames = Enum.GetNames<IntoleranceLevel>();
        var intoleranceLevelPrompt = $"Choose from {string.Join(", ", intoleranceLevelNames)}";
        Task<ChatCompletion<T>> Search<T>(string prompt) => chatClient.CompleteAsync<T>(prompt);
        async Task<SensitivityLevel> GetSensitivityLevel(string categoryName)
        {
            var prompt = $"What is the intolerance level for {categoryName}? {intoleranceLevelPrompt}";
            var levelCompletion = await Search<IntoleranceLevel>(prompt);
            var level = levelCompletion.Result;
            var category = Enum.Parse<SensitivityCategory>(categoryName);
            return new(category, level);
        }
        var sensitivities = await sensitivityCategoryNames
            .ToAsyncEnumerable()
            .SelectAwait(async x => await GetSensitivityLevel(x))
            .Where(x => ((int)x.Sensitivity & (~0)) != 0)
            .Where(x => x.IntoleranceLevel != IntoleranceLevel.None)
            .ToArrayAsync();

        return new(foodName, sensitivities);
    }
}
