using System.Text.Json.Serialization;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class Build
{
    public Build() { }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string BuildNumber { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; set; } = string.Empty;

    public override string ToString()
    {
        return @$"ID: {Id}
BuildNumber: {BuildNumber}
URL: {Url}
SourceBranch: {SourceBranch}
";
    }
}
