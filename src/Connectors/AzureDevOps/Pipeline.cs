using System.Collections.Generic;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class Pipeline
{
    public Pipeline() { }

    public int Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public int Revision { get; set; }

    public string Name { get; set; } = string.Empty;

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

/*
All:
    pipelines?
        orderBy={orderBy}&$top={$top}&continuationToken={continuationToken}

Single:
    pipelines/{pipelineId}?
        pipelineVersion={pipelineVersion}
*/
public class PipelineOptions
{
    public int? PipelineId { get; set; }
    public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
}