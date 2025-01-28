using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using App;

var host = Startup.CreateHost();
T Get<T>() where T : notnull => host.Services.GetRequiredService<T>();
var fodmapPlugin = Get<FodmapResearchPlugin>();
var searchReasoningAgent = Get<SearchReasoningAgent>();
using var chat = Get<IChatClient>()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var result = await chat.CompleteAsync("Is chocolate a FODMAP?", options: new()
{
    ToolMode = ChatToolMode.Auto,
    Tools =
    [
        AIFunctionFactory.Create(fodmapPlugin.ResearchFoodSensitivityAsync),
        AIFunctionFactory.Create(searchReasoningAgent.SearchAndSummarizeAsync),
    ]
});
Console.WriteLine(result);
Console.ReadLine(); // Wait for user input before closing

await host.RunAsync();
