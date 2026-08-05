using Impersonate.Application;
using Impersonate.Infrastructure;
using Impersonate.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHostedService<FoundationWorker>();
builder.Services.AddHostedService<TaskExecutionWorker>();
builder.Services.AddHostedService<TaskDeliveryWorker>();
builder.Services.AddHostedService<TaskDeliveryReconciliationWorker>();
builder.Services.AddHostedService<TaskDeliveryReviewWorker>();
builder.Services.AddHostedService<TaskDeliveryRepairWorker>();
builder.Services.AddHostedService<TaskDeliveryIntegrationWorker>();

var host = builder.Build();
host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DataProtection").LogInformation("Data Protection key ring: {DataProtectionKeyRingPath}", host.Services.GetRequiredService<Impersonate.Infrastructure.Ai.DataProtectionKeyRingLocation>().Path);
await host.RunAsync();
