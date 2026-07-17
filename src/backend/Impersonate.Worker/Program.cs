using Impersonate.Application;
using Impersonate.Infrastructure;
using Impersonate.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<FoundationWorker>();

var host = builder.Build();
await host.RunAsync();
