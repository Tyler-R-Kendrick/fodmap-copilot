using Microsoft.Extensions.Logging;

namespace App;

public class SearchReasoningAgent(
    PerplexitySearchPlugin searchPlugin,
    ILogger<SearchReasoningAgent> logger)
{
    public async Task<PerplexitySearchPlugin.SearchResult> SearchAndSummarizeAsync(
        string query)
    {
        logger.LogInformation("Searching and summarizing for {query}...", query);
        var searchResponse = await searchPlugin.SearchAsync(query);
        logger.LogInformation("Summarized search results for {query}: {summarizedResponses}", query, searchResponse);
        return searchResponse;
    } 
}
