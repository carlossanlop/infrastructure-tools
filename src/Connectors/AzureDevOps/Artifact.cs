using System.Text.Json.Serialization;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class Artifact
{
    public Artifact() { }

    [JsonPropertyName("id")]
    public int Id { get;set; }

    [JsonPropertyName("name")]
    public string Name { get;set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get;set; } = string.Empty;

    [JsonPropertyName("resource")]
    public ArtifactResource? Resource { get; set; }

    public override string ToString()
    {
        return @$"ID: {Id}
Name: {Name}
Source: {Source}
Resource:
    Type: {Resource?.Type}
    Data: {Resource?.Data}
    Properties:
        LocalPath: {Resource?.Properties?.LocalPath}
        ArtifactSize: {Resource?.Properties?.ArtifactSize}
    Url: {Resource?.Url}
    DownloadUrl: {Resource?.DownloadUrl}
";
    }
}

public class ArtifactResource
{
    public ArtifactResource() { }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public ArtifactResourceProperties? Properties { get; set; }
}

public class ArtifactResourceProperties
{
    public ArtifactResourceProperties() { }

    [JsonPropertyName("localpath")]
    public string LocalPath { get; set; } = string.Empty;

    [JsonPropertyName("artifactsize")]
    public string ArtifactSize { get; set; } = string.Empty;
}