using InfrastructureTools.CommitCollector;
using Octokit;

namespace InfrastructureTools.CommitCollector.Tests;

public class CommitCollectorTests
{
    private readonly CommitCollector _collector;
    private readonly string _org = "dotnet";
    private readonly string _repo = "runtime";

    public CommitCollectorTests()
    {
        Assert.True(File.Exists(CommitCollector.DefaultConfigJsonFilePath), $"Default config json file path does not exist: {CommitCollector.DefaultConfigJsonFilePath}");
        _collector = CommitCollector.CreateAsync(CommitCollector.DefaultConfigJsonFilePath, _org, _repo, askForOptions: false).Result;
    }

    [Fact]
    public async Task TestCommit()
    {
        // From https://github.com/dotnet/runtime/pull/111376/commits

        string sha = "b2ac274f8d889ce931f87051412ac14ca8ab143b";
        IReadOnlyList<PullRequestCommit> prCommits = await _collector.Client.PullRequest.Commits(_org, _repo, 111376);
        PullRequestCommit prCommit = prCommits.FirstOrDefault(c => c.Sha == sha);
        Assert.NotNull(prCommit);

        List<(PullRequestCommit, GitHubCommit)> included = new();
        List<(PullRequestCommit, string)> skipped = new();
        _collector.ProcessPullRequestCommit(prCommit, included, skipped);
        Assert.True(included.Count == 1);
        Assert.True(skipped.Count == 0);
        Assert.Contains(included, x => x.Item1.Sha == sha);
    }
}
