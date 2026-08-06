using System.Text.Json.Serialization;
using Impersonate.Application;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Quality;
using Impersonate.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ITaskControlService, TaskControlService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IProjectQualityService, ProjectQualityService>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("FrontendDevelopment", policy => policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
var app = builder.Build();
app.Lifetime.ApplicationStopping.Register(() => app.Logger.LogInformation("Impersonate API cancellation started."));
app.Lifetime.ApplicationStopped.Register(() => app.Logger.LogInformation("Impersonate API shutdown completed."));
app.Logger.LogInformation("Data Protection key ring: {DataProtectionKeyRingPath}", app.Services.GetRequiredService<Impersonate.Infrastructure.Ai.DataProtectionKeyRingLocation>().Path);
app.Logger.LogInformation("Starting Impersonate API");
if (app.Environment.IsDevelopment())
{
    app.UseCors("FrontendDevelopment");
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Impersonate API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapHealthChecks("/health");
app.MapControllers();
app.Run();

public partial class Program;
