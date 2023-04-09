using System.Text.Json.Serialization;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class Pipeline
{
    public Pipeline() { }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = string.Empty;

    public override string ToString()
    {
        return @$"ID: {Id}
Name: {Name}
Folder: {Folder}
URL: {Url}
Revision: {Revision}
";
    }
}
