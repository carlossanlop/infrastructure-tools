using InfrastructureTools.CommitCollector;
using Octokit;

namespace InfrastructureTools.CommitCollector;

public record PullRequestInformation(
    PullRequestCommit PRCommit,
    GitHubCommit GHCommit,
    PullRequest PR,
    PullRequestPeople People
);