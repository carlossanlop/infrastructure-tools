using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class PullRequest
{
    public PullRequest() { }

    public int PullRequestId { get; set; }

    public PullRequestStatus Status { get; set; }

    [JsonPropertyName("createdBy.displayName")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreationDate { get; set; }

    public string Title { get; set; } = string.Empty;

    public override string ToString()
    {
        return @$"{PullRequestId} - {Title}
Created: {CreationDate}
Creator: {CreatedBy}
Status: {Status}
";
    }
}

public enum PullRequestStatus
{
    Abandoned,
    Active,
    All,
    Completed,
    NotSet
}

/*
All:
    repositories/{repositoryId}/pullrequests?
        searchCriteria.creatorId={searchCriteria.creatorId}&
        searchCriteria.includeLinks={searchCriteria.includeLinks}&
        searchCriteria.repositoryId={searchCriteria.repositoryId}&
        searchCriteria.reviewerId={searchCriteria.reviewerId}&
        searchCriteria.sourceRefName={searchCriteria.sourceRefName}&
        searchCriteria.sourceRepositoryId={searchCriteria.sourceRepositoryId}&
        searchCriteria.status={searchCriteria.status}&
        searchCriteria.targetRefName={searchCriteria.targetRefName}&
        maxCommentLength={maxCommentLength}&
        $skip={$skip}&
        $top={$top}
Single:
    git/pullrequests/{pullRequestId}
or
    git/repositories/{repositoryId}/pullrequests/{pullRequestId}?
    maxCommentLength={maxCommentLength}&$skip={$skip}&$top={$top}&
    includeCommits={includeCommits}&includeWorkItemRefs={includeWorkItemRefs}
*/
public class PullRequestOptions
{
    public string Repo { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = string.Empty;
    public PullRequestStatus Status { get; set; } = PullRequestStatus.Completed;
    public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
}
