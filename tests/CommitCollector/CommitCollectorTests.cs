using Octokit;

namespace InfrastructureTools.CommitCollector.Tests;

public class CommitCollectorTests
{
    private const string _org = "dotnet";
    private const string _repo = "runtime";

    private static readonly Lazy<CommitCollector> _collector = new(() =>
    {
        Assert.True(File.Exists(CommitCollector.DefaultConfigJsonFilePath), $"Default config json file path does not exist: {CommitCollector.DefaultConfigJsonFilePath}");
        return CommitCollector.CreateAsync(CommitCollector.DefaultConfigJsonFilePath, _org, _repo, askForOptions: false).Result;
    });

    // Manual flow 8.0 PR: https://github.com/dotnet/runtime/pull/111376/commits/
    private readonly Lazy<Task<IReadOnlyList<PullRequestCommit>>> _prCommits111376 = new(() =>
        _collector.Value.Client.PullRequest.Commits(_org, _repo, 111376));

    // Manual flow 9.0 PR: https://github.com/dotnet/runtime/pull/112382/commits/
    private readonly Lazy<Task<IReadOnlyList<PullRequestCommit>>> _prCommits112382 = new(() =>
        _collector.Value.Client.PullRequest.Commits(_org, _repo, 112382));

    // Manual flow 9.0 PR: https://github.com/dotnet/runtime/pull/113350/commits/
    private readonly Lazy<Task<IReadOnlyList<PullRequestCommit>>> _prCommits113350 = new(() =>
        _collector.Value.Client.PullRequest.Commits(_org, _repo, 113350));

    // Manual flow 9.0 PR: https://github.com/dotnet/runtime/pull/111378/commits/
    private readonly Lazy<Task<IReadOnlyList<PullRequestCommit>>> _prCommits111378 = new(() =>
        _collector.Value.Client.PullRequest.Commits(_org, _repo, 111378));

    #region Forbidden strings and patterns

    [Fact]
    public Task Commit_Skip_UpdateDependencies() =>
        // Commit for an "Update dependencies from" PR:
        // https://github.com/dotnet/runtime/pull/111376/commits/b2ac274f8d889ce931f87051412ac14ca8ab143b
        Commit_SkipAsync(_prCommits111376, sha: "6113028777e1533b6b4cc5164ce10f8dbb66925d", motive: "author: maestrobot", title: "Update dependencies from");

    [Fact]
    public Task Commit_Skip_MergeBranch() =>
        // Commit for a "Merge branch" PR:
        // https://github.com/dotnet/runtime/pull/112382/commits/ca6fe3d7e1461b49831db626424e1f17bc9d9a24
        Commit_SkipAsync(_prCommits112382, sha: "ca6fe3d7e1461b49831db626424e1f17bc9d9a24", motive: "Skip title text: Merge branch ", title: "Merge branch");

    [Fact]
    public Task Commit_Skip_MergePullRequest() =>
        // Commit for a "Merge pull request" PR:
        // https://github.com/dotnet/runtime/pull/112382/commits/e84969e47f7a22eb99ecd88c788db743e44d589e
        Commit_SkipAsync(_prCommits112382, sha: "e84969e47f7a22eb99ecd88c788db743e44d589e", motive: "Skip title pattern: Merge pull request ", title: "Merge pull request");

    [Fact]
    public Task Commit_Skip_MergRemoteTrackingBranch() =>
        // Commit for a "Merge remote tracking branch" PR:
        // https://github.com/dotnet/runtime/pull/113350/commits/a635ae1d3fbe1ccfc312f01a606ee9f790618b5e
        Commit_SkipAsync(_prCommits113350, sha: "a635ae1d3fbe1ccfc312f01a606ee9f790618b5e", motive: "Skip title text: Merge remote-tracking branch ", title: "Merge remote-tracking branch");

    #endregion

    #region Included

    [Fact]
    public Task Commit_Include_TrimReleaseBranchPrefixAndSuffix() =>
        // Commit for a PR that starts with [Release/8.0] and ends with the PR number in parenthesis
        // https://github.com/dotnet/runtime/pull/111376/commits/7db9d7b35e867fa310a618a7e413591839458722
        Commit_IncludeAsync(_prCommits111376, sha: "7db9d7b35e867fa310a618a7e413591839458722", originalTitle: "[Release/8.0] Fix FP state restore on macOS exception forwarding (#109579)", formattedTitle: "[Fix FP state restore on macOS exception forwarding](https://github.com/dotnet/runtime/pull/109579)", authors: "Jan Vorlicek / Jeff Schwartz, Vladimir Sadov");

    [Fact]
    public Task Commit_Include_ExcludeNonReviewers() =>
        // Commit for a PR that has 5 reviewers but only 2 signed off
        // https://github.com/dotnet/runtime/pull/111376/commits/eda4c706539eed06c4314e912dd0697433cf183e
        Commit_IncludeAsync(_prCommits111376, sha: "eda4c706539eed06c4314e912dd0697433cf183e", originalTitle: "Support step into a tail call (#110440)", formattedTitle: "[Support step into a tail call](https://github.com/dotnet/runtime/pull/110440)", authors: "Thays Grazia / Jeff Schwartz, Tom McDonald");

    #endregion

    #region Test files

    [Fact]
    public async Task Commit_Skip_OneTestOnlyFile()
    {
        // Commit for a PR that contains only one test file under src/libraries/*/tests/*
        // https://github.com/dotnet/runtime/pull/111378/commits/bbd71321eb6286d1297ebc3c67a0abd3a1da29ac
        await Commit_Skip_AllTestFiles(_prCommits111378, sha: "bbd71321eb6286d1297ebc3c67a0abd3a1da29ac");
        // Commit for a PR that contains only a csproj
        // https://github.com/dotnet/runtime/pull/111376/commits/1a0aa3bba0c2681b6084f921560311010f5ccf2c
        await Commit_Skip_AllTestFiles(_prCommits111376, sha: "1a0aa3bba0c2681b6084f921560311010f5ccf2c");
    }

    [Fact]
    public Task Commit_Skip_MultipleTestOnlyFiles() =>
        // Commit for a PR with a bunch of files under src/libraries/*/tests/*
        // https://github.com/dotnet/runtime/pull/111376/commits/d801ec54c539479bbd8d6f28d41190dbb4ec28de
        Commit_Skip_AllTestFiles(_prCommits111376, sha: "d801ec54c539479bbd8d6f28d41190dbb4ec28de");

    #endregion

    #region Infra files

    [Fact]
    public Task Commit_Skip_AllInfraFiles_PropsFile() =>
        // Commit for a PR with just a .props file:
        // https://github.com/dotnet/runtime/pull/113350/commits/67cbfaac4cf32cc0a47fbd4c3b1d02688a5a4ed4
        Commit_Skip_AllInfraFiles(_prCommits113350, sha: "67cbfaac4cf32cc0a47fbd4c3b1d02688a5a4ed4");

    [Fact]
    public Task Commit_Skip_AllInfraFiles_ShFile() =>
        // Commit for a PR with just a .sh file:
        // https://github.com/dotnet/runtime/pull/113350/commits/f4fd947a03efd877d6e3b4db38d54ac392494ce3
        Commit_Skip_AllInfraFiles(_prCommits113350, sha: "f4fd947a03efd877d6e3b4db38d54ac392494ce3");

    [Fact]
    public Task Commit_Skip_AllInfraFiles_CMakeListsTxtFile() =>
        // Commit for a PR with just a CMakeLists.txt file
        // https://github.com/dotnet/runtime/pull/111378/commits/f58036422045bc89fa2e70536be5a857614cfee8
        Commit_Skip_AllInfraFiles(_prCommits111378, sha: "f58036422045bc89fa2e70536be5a857614cfee8");

    #endregion

    #region Internal helpers

    private Task Commit_Skip_AllTestFiles(Lazy<Task<IReadOnlyList<PullRequestCommit>>> prCommits, string sha) =>
        Commit_SkipAsync(prCommits, sha, motive: "All test files");

    private Task Commit_Skip_AllInfraFiles(Lazy<Task<IReadOnlyList<PullRequestCommit>>> prCommits, string sha) =>
        Commit_SkipAsync(prCommits, sha, motive: "All infra files");

    private async Task Commit_SkipAsync(Lazy<Task<IReadOnlyList<PullRequestCommit>>> prCommits, string sha, string motive, string title = null)
    {
        IReadOnlyList<PullRequestCommit> list = await prCommits.Value;
        Assert.NotEmpty(list);
        PullRequestCommit prCommit = list.FirstOrDefault(c => c.Sha == sha);
        Assert.NotNull(prCommit);

        if (title != null)
        {
            Assert.Contains(title, prCommit.Commit.Message);
        }

        List<(PullRequestCommit, GitHubCommit)> included = new();
        List<(PullRequestCommit, string)> skipped = new();
        _collector.Value.ProcessPullRequestCommit(prCommit, included, skipped);
        Assert.Empty(included);
        Assert.Single(skipped);
        Assert.Empty(_collector.Value.Errors);
        (PullRequestCommit skippedPRCommit, string skippedMsg) = skipped.First();
        Assert.Equal(sha, skippedPRCommit.Sha);
        Assert.Equal(motive, skippedMsg);
    }

    private async Task Commit_IncludeAsync(Lazy<Task<IReadOnlyList<PullRequestCommit>>> prCommits, string sha, string originalTitle, string formattedTitle, string authors)
    {
        IReadOnlyList<PullRequestCommit> list = await prCommits.Value;
        Assert.NotEmpty(list);
        PullRequestCommit prCommit = list.FirstOrDefault(c => c.Sha == sha);
        Assert.NotNull(prCommit);

        Assert.Contains(originalTitle, prCommit.Commit.Message);

        List<(PullRequestCommit, GitHubCommit)> included = new();
        List<(PullRequestCommit, string)> skipped = new();
        _collector.Value.ProcessPullRequestCommit(prCommit, included, skipped);
        Assert.Single(included);
        Assert.Empty(skipped);
        Assert.Empty(_collector.Value.Errors);

        (PullRequestCommit includedPRCommit, GitHubCommit includedGitHubCommit) = included.First();
        Assert.Equal(sha, includedPRCommit.Sha);

        string[] table = _collector.Value.GetIncludedTable(included).ToString().Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // Exclude the header and the header divider
        Assert.Equal(table.Length - 2, included.Count);

        string[] columns = table[2].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, columns.Length); // Comments and Validation Status are empty, so they should get trimmed

        Assert.Equal(formattedTitle, columns[0]);
        Assert.Equal(authors, columns[1]);
    }

    #endregion
}
