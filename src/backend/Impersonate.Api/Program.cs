using Impersonate.Application;
using Impersonate.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.Logger.LogInformation("Starting Impersonate API");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { Name = "Impersonate API", Status = "Running" }));
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
