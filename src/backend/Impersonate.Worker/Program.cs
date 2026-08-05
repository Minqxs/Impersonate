using Impersonate.Application;
using Impersonate.Infrastructure;
using Impersonate.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
builder.Services.AddHostedService<FoundationWorker>();
builder.Services.AddHostedService<TaskExecutionWorker>();
builder.Services.AddHostedService<TaskDeliveryWorker>();
builder.Services.AddHostedService<TaskDeliveryReconciliationWorker>();
builder.Services.AddHostedService<TaskDeliveryReviewWorker>();
builder.Services.AddHostedService<TaskDeliveryRepairWorker>();
builder.Services.AddHostedService<TaskDeliveryIntegrationWorker>();
builder.Services.AddHostedService<FinalRunReviewWorker>();
builder.Services.AddHostedService<FinalPullRequestWorker>();

var host = builder.Build();
var lifecycleLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WorkerLifecycle");
host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => lifecycleLogger.LogInformation("Worker cancellation started."));
host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(() => lifecycleLogger.LogInformation("Worker shutdown completed."));
host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DataProtection").LogInformation("Data Protection key ring: {DataProtectionKeyRingPath}", host.Services.GetRequiredService<Impersonate.Infrastructure.Ai.DataProtectionKeyRingLocation>().Path);
await host.RunAsync();
