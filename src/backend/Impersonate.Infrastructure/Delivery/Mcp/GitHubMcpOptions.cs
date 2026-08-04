namespace Impersonate.Infrastructure.Delivery.Mcp;

public sealed class GitHubMcpOptions
{
    public bool Enabled
    {
        get; set;
    }
    public string Transport { get; set; } = "Remote";
    public string ServerId { get; set; } = "github-official";
    public string RemoteUrl { get; set; } = "https://api.githubcopilot.com/mcp/";
    public string TokenEnvironmentVariable { get; set; } = "GITHUB_MCP_TOKEN";
    public string LocalCommand { get; set; } = "github-mcp-server";
    public string[] LocalArguments { get; set; } = ["stdio"];
    public string[] AllowedRepositories { get; set; } = [];
    public string[] Tools { get; set; } = ["list_pull_requests", "pull_request_read", "create_pull_request"];
    public int TimeoutSeconds { get; set; } = 60;
    public bool DraftPullRequests { get; set; } = true;
}
