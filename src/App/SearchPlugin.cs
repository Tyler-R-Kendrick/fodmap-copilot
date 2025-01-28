using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.CognitiveServices.Search.WebSearch;
using Microsoft.Azure.CognitiveServices.Search.WebSearch.Models;
using System.Text.Json;

namespace App;


public class PerplexitySearchPlugin(
    OpenAI.Chat.ChatClient chatClient,
    ILogger<PerplexitySearchPlugin> logger)
{    
    [Description("The search response for the given query.")]
    public record SearchResult(
        [Description("The search result summary.")]
        string Summary,
        [Description("The citations of the sources used in the summary.")]
        Uri[] Citations)
    {
        public override string? ToString()
            => $"{Summary}\n\n{string.Join<Uri>("\n\n", Citations)}";
    }

    public async Task<SearchResult> SearchAsync(string query)
    {
        logger.LogInformation("Searching for {query}...", query);
        var response = await chatClient.CompleteChatAsync(query);
        logger.LogInformation("Found search result for {query}: {response}", query, response.Value.ToString());
#pragma warning disable AOAI001
        var rawResponse = response.GetRawResponse() ?? throw new InvalidOperationException("No raw response found.");
        var output = rawResponse.Content;
        using JsonDocument outputAsJson = JsonDocument.Parse(output.ToString());
        logger.LogInformation("Output as JSON: {outputAsJson}", outputAsJson);
        var citationsProperty = outputAsJson.RootElement
            .GetProperty("citations"u8);

        var citations = citationsProperty.EnumerateArray()
            .Select(c => c.GetString())
            .Where(c => c != null)
            .Select(c => new Uri(c!))
            .ToArray();
        logger.LogInformation("Citations: {citations}", citations);
        //var messageContext = completion.GetMessageContext() ?? throw new InvalidOperationException("No message context found.");
        //var citations = messageContext.Citations.Select(c => new Uri(c.Url)).ToArray();
#pragma warning restore AOAI001
        var responseString = response.ToString() ?? "No response found.";
        return new(responseString, citations);
    }
}


[Description("Search the web for a given query.")]
public class SearchPlugin(
    IChatClient chatClient,
    IWebSearchClient searchClient,
    HttpClient httpClient,
    ILogger<SearchPlugin> logger)
{
    [return: Description("The search response for the given query.")]
    [Description("Search the web for a given query.")]
    public async Task<SearchResponse> SearchAsync(
        [Description("The query to search for.")]
        string query)
    {
        logger.LogInformation("Searching for {query}...", query);
        var result = await searchClient.Web.SearchAsync(query);
        logger.LogInformation("Found search result for {query}: {result}", query, result);
        return result;
    }

    [return: Description("The summarized web responses.")]
    [Description("Summarize web responses.")]
    public async Task<CitedWebResponses> SummarizeWebResponsesAsync(
        [Description("The web responses to summarize.")]
        CitedWebResponse[] webResponses)
    {
        logger.LogInformation("Summarizing web responses...");
        var aggregate = string.Join("\n", webResponses.Select(x => x.WebResponse));
        var prompt = $"Given the following web responses:\n{aggregate}\n\nSummarize the responses in a short summary.";
        var summaryResponse = await chatClient.CompleteAsync(prompt);
        var summary = summaryResponse.ToString();
        logger.LogInformation("Summarized web responses: {summary}", summary);
        return new(summary, webResponses);
    }

    [return: Description("The web response as a string and citation tuple pairing.")]
    [Description("Gets a summary of a search result with citations.")]
    public async IAsyncEnumerable<CitedWebResponse> GetWebResponseAsync(
        [Description("The search response to reason about for a response.")]
        SearchResponse searchResponse,
        [Description("The number of top search results to consider.")]
        int topN = 3)
    {
        logger.LogInformation("Getting web responses...");
        var originalQuery = searchResponse.QueryContext.OriginalQuery;
        var webPages = searchResponse.WebPages.Value.Take(topN);
        foreach (var page in webPages)
        {
            var url = page.Url;
            var citation = new Uri(url);
            var pageContent = await httpClient.GetStringAsync(url);
            var prompt = $@"Given the following content:
            {pageContent}
            
            What is the answer to the question: {originalQuery}?
            Provide your response as a brief summary related to the question.";
            logger.LogInformation("Requesting web response for {originalQuery}...", originalQuery);
            var pageSummary = await chatClient.CompleteAsync(prompt);
            logger.LogInformation("Found web response for {originalQuery}: {response}", originalQuery, pageSummary);
            var response = pageSummary.ToString();
            yield return new(response, citation);
        }
    }

    [Description("The web response as a string and citation tuple pairing.")]
    public record CitedWebResponse(
        [Description("The web response as a string.")]
        string WebResponse,
        [Description("The citation for the web response.")]
        Uri Citation);

    [Description("The web responses as a string and citation tuple pairing.")]
    public record CitedWebResponses(
        [Description("A summary of the aggregated responses.")]
        string Summary,
        [Description("The web responses as a string and citation tuple pairing.")]
        CitedWebResponse[] WebResponses)
    {
        public override string? ToString()
            => $"{Summary}\n\n{string.Join("\n\n", WebResponses.Select(x => x.Citation))}";
    }
}
