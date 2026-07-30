using System.Text.Json.Serialization;
using Impersonate.Application;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure;

public sealed record ModelSelectionPreviewRequest(AgentRole Role, string Description, Guid? ManualModelOverrideId);
