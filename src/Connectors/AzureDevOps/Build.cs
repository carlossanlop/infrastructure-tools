using System.Collections.Generic;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class Build
{
    public Build() { }

    public int Id { get; set; }
    public string BuildNumber { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
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

/*
All:
    build/builds?
        definitions={definitions}&queues={queues}&buildNumber={buildNumber}&minTime={minTime}&maxTime={maxTime}
        &requestedFor={requestedFor}&reasonFilter={reasonFilter}&statusFilter={statusFilter}&resultFilter={resultFilter}
        &tagFilters={tagFilters}&properties={properties}&$top={$top}&continuationToken={continuationToken}
        &maxBuildsPerDefinition={maxBuildsPerDefinition}&deletedFilter={deletedFilter}&queryOrder={queryOrder}
        &branchName={branchName}&buildIds={buildIds}&repositoryId={repositoryId}&repositoryType={repositoryType}
Single:
    build/builds/{buildId}?
        propertyFilters={propertyFilters}
*/
public class BuildOptions
{
    public int? BuildId { get; set; }
    public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
}