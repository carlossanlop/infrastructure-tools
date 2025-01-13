namespace InfrastructureTools.Connectors.GitHub;

public class GitHubOptions
{
    public string AppName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
}