using System.Collections.Generic;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class BuildDefinition
{
    public BuildDefinition() { }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string QueueStatus { get; set; } = string.Empty;
    public int Revision { get; set; }
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

/*
All:
    build/definitions?
        name={name}&repositoryId={repositoryId}&repositoryType={repositoryType}&
        queryOrder={queryOrder}&$top={$top}&continuationToken={continuationToken}&
        minMetricsTime={minMetricsTime}&definitionIds={definitionIds}&path={path}&
        builtAfter={builtAfter}&notBuiltAfter={notBuiltAfter}&includeAllProperties={includeAllProperties}&
        includeLatestBuilds={includeLatestBuilds}&taskIdFilter={taskIdFilter}&
        processType={processType}&yamlFilename={yamlFilename}
Single:
    build/definitions/{definitionId}?
        revision={revision}&minMetricsTime={minMetricsTime}&propertyFilters={propertyFilters}&
        includeLatestBuilds={includeLatestBuilds}

*/
public class BuildDefinitionOptions
{
    public string? BuildDefinitionName { get; set; }
    public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
}