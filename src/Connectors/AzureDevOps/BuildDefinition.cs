using System.Text.Json.Serialization;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class BuildDefinition
{
    public BuildDefinition() { }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("queueStatus")]
    public string QueueStatus { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("createdDate")]
    public string CreatedDate { get; set; } = string.Empty;

    public override string ToString()
    {
        return $@"ID: {Id}
Name: {Name}
URL: {Url}
Path: {Path}
Type: {Type}
QueueStatus: {QueueStatus}
Revision: {Revision}
CreatedDate: {CreatedDate}
";
    }
}
