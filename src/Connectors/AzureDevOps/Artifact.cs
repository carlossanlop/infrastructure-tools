using System.Collections.Generic;
using System.Threading.Tasks;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class Artifact
{
    public Artifact() { }

    public int Id { get;set; }
    public string Name { get;set; } = string.Empty;
    public string Source { get;set; } = string.Empty;
    public ArtifactResource? Resource { get; set; } = new();

    public override string ToString()
    {
        return @$"ID: {Id}
Name: {Name}
Source: {Source}
Resource: {Resource}
";
    }
}

public class ArtifactResource
{
    public ArtifactResource() { }

    public string Type { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public ArtifactResourceProperties? Properties { get; set; } = new();

    public override string ToString()
    {
        return @$"
    Type: {Type}
    Data: {Data}
    Url: {Url}
    DownloadUrl: {DownloadUrl}
    Properties: {Properties}";
    }
}

public class ArtifactResourceProperties
{
    public ArtifactResourceProperties() { }

    public string LocalPath { get; set; } = string.Empty;
    public string RootId { get; set; } = string.Empty;
    public string ArtifactSize { get; set; } = string.Empty;

    public override string ToString()
    {
        return $@"
        LocalPath: {LocalPath}
        RootId: {RootId}
        ArtifactSize: {ArtifactSize}";
    }
}

/*
All:
    build/builds/{buildId}/artifacts
Single:
    build/builds/{buildId}/artifacts?artifactName={artifactName}
*/
public class ArtifactsOptions
{
    public int BuildNumber { get; set; }
    public string? ArtifactName { get; set; }
    public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
}

