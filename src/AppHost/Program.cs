DistributedApplicationOptions options = new();
DistributedApplicationBuilder builder = new(options);

var api = builder.AddProject<Projects.API>("api");
builder.AddProject<Projects.App>("app")
    .WithReference(api);

var host = builder.Build();

await host.RunAsync();
