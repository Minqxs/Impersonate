using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public sealed record TaskDeliveryDto(Guid Id, TaskDeliveryStatus Status, string? BranchName, string? CommitSha, string? RemoteName, string? RemoteRepository, string? RemoteBranchName, string? PushedCommitSha, DateTimeOffset? PushedAtUtc, string? PullRequestProvider, string? PullRequestRepository, long? PullRequestNumber, string? PullRequestUrl, string? FailureCode, string? FailureMessage);
