using System.ComponentModel;
using Microsoft.Extensions.AI;
using OpenAI;
using Microsoft.Extensions.Logging;
using static App.SearchPlugin;
using Microsoft.Azure.CognitiveServices.Search.WebSearch.Models;

namespace App;

[Description("Research food sensitivities for a given food name.")]
public class FodmapResearchPlugin(
    IChatClient chatClient,
    SearchReasoningAgent searchReasoningAgent,
    ILogger<FodmapResearchPlugin> logger)
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
        IntoleranceLevel IntoleranceLevel,
        [Description("The source of the information.")]
        Uri[] Citations);
    
    [Description("The food name and its associated sensitivity levels.")]
    public record FoodSensitivity(
        [Description("The name of the food being researched.")]
        string FoodName,
        [Description("The sensitivity levels for the food.")]
        SensitivityLevel[] SensitivityLevels,
        [Description("The source of the information.")]
        Uri[] Citations);

    [Description("Research Food sensitivities for a given food name.")]
    public async Task<FoodSensitivity> ResearchFoodSensitivityAsync(
        [Description("The name of the food being researched.")] string foodName)
    {
        logger.LogInformation("Researching food sensitivities...");
        var sensitivityCategoryNames = Enum.GetNames<SensitivityCategory>();
        var intoleranceLevelNames = Enum.GetNames<IntoleranceLevel>();
        Task<PerplexitySearchPlugin.SearchResult> Search(string prompt) => searchReasoningAgent.SearchAndSummarizeAsync(prompt);
        async Task<SensitivityLevel> GetSensitivityLevel(string categoryName)
        {
            try
            {
                logger.LogDebug("Requesting intolerance level for {categoryName}...", categoryName);
                var intolerantFoodPrompt = $"What is the intolerance level for {categoryName} in {foodName}?";
                var prompt = $"{intolerantFoodPrompt}";
                var citedResponses = await Search(prompt);
                var classifierPrompt = $@"Based on the following: {citedResponses.Summary},
                    What are the details for {foodName}'s classifcation: {categoryName}?";
                var jsonSchema = $@"
                {{
                    ""type"": ""object"",
                    ""properties"": {{
                        ""sensitivity"": {{
                            ""type"": ""string"",
                            ""enum"": {sensitivityCategoryNames.Select(x => $"\"{x}\"").ToArray()}
                        }},
                        ""intoleranceLevel"": {{
                            ""type"": ""string"",
                            ""enum"": {intoleranceLevelNames.Select(x => $"\"{x}\"").ToArray()}
                        }},
                        ""citations"": {{
                            ""type"": ""array"",
                            ""items"": {{
                                ""type"": ""string"",
                                ""format"": ""uri""
                            }}
                        }}
                    }},
                    ""required"": [""sensitivity"", ""intoleranceLevel"", ""citations""]
                }}";
                var sensitivityCompletion = await chatClient.CompleteAsync<SensitivityLevel>(classifierPrompt, new ChatOptions()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(jsonSchema),
                });
                var sensitivity = sensitivityCompletion.Result;
                logger.LogDebug("Found sensitivity level for {foodName} {categoryName}: {intolerance}",
                    foodName, categoryName, sensitivity);
                return sensitivity;
            }
            catch (ErrorResponseException ex)
            {
                var inner = ex.GetBaseException();
                var helpLink = ex.Request.RequestUri;
                logger.LogError("{inner} {helpLink}", inner, helpLink);
                return new(SensitivityCategory.None, IntoleranceLevel.None, []);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get intolerance level for {categoryName} for {foodName}.", categoryName, foodName);
                return new(SensitivityCategory.None, IntoleranceLevel.None, []);
            }
        }
        try
        {
            var sensitivities = await sensitivityCategoryNames
                .Where(x => x != SensitivityCategory.None.ToString())
                .ToAsyncEnumerable()
                .SelectAwait(async x => await GetSensitivityLevel(x))
                .Where(x => x.Sensitivity != SensitivityCategory.None)
                .Where(x => x.IntoleranceLevel != IntoleranceLevel.None)
                .ToArrayAsync();
            logger.LogInformation("Found food sensitivities for {foodName}: {sensitivities}", foodName, sensitivities);
            var citations = sensitivities.SelectMany(x => x.Citations).ToArray();
            return new(foodName, sensitivities, citations);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to research food sensitivities for {foodName}.", foodName);
            throw;
        }
    }
}
