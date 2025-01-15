using InfrastructureTools.Shared;
using InfrastructureTools.Connectors.GitHub;
using Octokit;
using InfrastructureTools.MarkdownTable;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfrastructureTools.CommitCollect;

public class CommitCollector
{
    private const string UserNameMaestroBot = "dotnet-maestro[bot]";
    private const string UserNameGitHubActionsBot = "github-actions[bot]";
    private readonly string[] NewLines = ["\n", "\r\n"];
    private readonly string[] InfraExtensions = ["CMakeLists.txt", ".cmake", ".config", ".csproj", ".editorconfig", ".gitignore", ".ilproj", ".json", ".md", ".pp", ".proj", ".props", ".ps1", ".ruleset", ".sln", ".targets", ".txt", ".xml", ".yml"];
    private readonly string[] ForbiddenStrings = [
        "Update dependencies from ",
        "Merge branch "
    ];
    private readonly string[] ForbiddenPatterns = [
        @"Merge pull request (dotnet)?\#\d+ from dotnet/merge/release/"
    ];
    private readonly string[] StringsToTrim = [
        "[release/8.0] ",
        "[release/9.0] ",
        "[release/8.0-staging] ",
        "[release/9.0-staging] ",
    ];
    private readonly string[] StringPatternsToTrim = [
        @"[ ]*\(\#\d+\)",
        @"\[\d+\.0\] "
    ];

    private readonly GitHubClient _client;
    private string? _org;
    private string? _repo;

    private CommitCollector(GitHubClient client)
    {
        _client = client;
    }

    public static async Task<CommitCollector> CreateAsync()
    {
        string tmpFilePath = Path.Join(Path.GetTempPath(), "infrastructure-tools-settings.json");

        string? optionsFilePath;
        if (!File.Exists(tmpFilePath))
        {
            Console.Write("Enter the path to the GitHub options file path: ");
            optionsFilePath = Console.ReadLine();
            ArgumentException.ThrowIfNullOrEmpty(optionsFilePath);
            if (!File.Exists(optionsFilePath))
            {
                throw new FileNotFoundException("GitHub options file not found.", optionsFilePath);
            }
        }
        else
        {
            optionsFilePath = tmpFilePath;
            Console.WriteLine($"Found pre-existing GitHub options file: {tmpFilePath}. If retrieving the client fails, update or delete that file and run this tool again.");
        }

        GitHubClient client = await GitHubAuthenticator.GetClientAsync(optionsFilePath);
        if (optionsFilePath != tmpFilePath)
        {
            File.Copy(optionsFilePath, tmpFilePath);
        }

        return new CommitCollector(client);
    }

    public void Run(string org, string repo, int prNumber)
    {
        _org = org;
        _repo = repo;

        IReadOnlyList<PullRequestCommit> commits = _client.PullRequest.Commits(_org, _repo, prNumber).Result;

        List<(string, string)> skipped = new();

        var table = new MarkdownTableBuilder().WithHeader("PR", "Author/Approvers", "Comments", "Validation status");

        foreach (PullRequestCommit commit in commits)
        {
            string firstMessageLine = GetFirstLine(commit.Commit.Message);
            GitHubCommit ghCommit = _client.Repository.Commit.Get(_org, _repo, commit.Sha).Result;
            if (IsSkippable(firstMessageLine, ghCommit, out string? reason))
            {
                skipped.Add((reason, firstMessageLine));
            }
            else
            {
                firstMessageLine = RemoveTexts(firstMessageLine);
                (PullRequest? pr, Dictionary<string, string> people) = GetPullRequestAuthorAndApprovers(ghCommit);
                string url = pr == null ? ghCommit.HtmlUrl : pr.HtmlUrl;
                table.WithRow($"[{firstMessageLine}]({url})", $"{string.Join(", ", people.Values)}", string.Empty /* Comments */,  string.Empty /* Validation status */);
            }
        }


        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
        ConsoleLog.WriteSuccess(table.ToString());
        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();

        var skippedTable = new MarkdownTableBuilder().WithHeader("Reason", "Title");

        ConsoleLog.WriteFailure("Commits that were skipped:");
        foreach ((string reason, string firstLine) in skipped)
        {
            skippedTable = skippedTable.WithRow(reason, firstLine);
        }
        ConsoleLog.WriteFailure(skippedTable.ToString());
    }

    private (PullRequest?, Dictionary<string, string>) GetPullRequestAuthorAndApprovers(GitHubCommit commit)
    {
        string firstLine = GetFirstLine(commit.Commit.Message);

        Dictionary<string, string> people = new(); // alias -> name

        Match matchPrNumberInCommitTitle = Regex.Match(firstLine, @"\(\#(?<prNumber>\d+)\)");

        if (!matchPrNumberInCommitTitle.Success)
        {
            return (null, people);
        }

        int prNumber = int.Parse(matchPrNumberInCommitTitle.Groups["prNumber"].Value);

        if (!TryGetPrAndAddPeople(people, prNumber, out PullRequest? pr))
        {
            return (null, people);
        }

        if (commit.Commit.Author.Name == UserNameGitHubActionsBot)
        {
            // The initial PR is a backport PR
            Match matchOriginalPrNumberInBackportBody = Regex.Match(pr.Body, @"(Backport of|Backports) \#(?'originalPrNumber'\d+)");
            if (!matchOriginalPrNumberInBackportBody.Success)
            {
                return (pr, people);
            }

            int actualPrNumber = int.Parse(matchOriginalPrNumberInBackportBody.Groups["originalPrNumber"].Value);
            if (!TryGetPrAndAddPeople(people, actualPrNumber, out PullRequest? actualPr))
            {
                return (pr, people);
            }

            // Only one more level check, in case it's a backport of another backport
            if (actualPr.User.Login == UserNameGitHubActionsBot)
            {
                Match matchfirstPrLink = Regex.Match(actualPr.Body, @"\#(?'secondBackportPrNumber'\d+)");
                if (!matchfirstPrLink.Success)
                {
                    return (actualPr, people);
                }

                int secondBackportPrNumber = int.Parse(matchfirstPrLink.Groups["secondBackportPrNumber"].Value);
                if (!TryGetPrAndAddPeople(people, secondBackportPrNumber, out PullRequest? SecondPr))
                {
                    return (actualPr, people);
                }
                pr = SecondPr;
            }
        }


        return (pr, people);
    }

    private bool TryGetPrAndAddPeople(Dictionary<string, string> people, int prNumber, [NotNullWhen(returnValue: true)] out PullRequest? pr)
    {
        try
        {
            pr = _client.PullRequest.Get(_org, _repo, prNumber).Result;
            AddPeople(people, pr);
            return pr != null;
        }
        catch {}

        pr = null;
        return false;
    }

    private void AddPeople(Dictionary<string, string> people, PullRequest pr)
    {
        if (pr.Assignee.Login != UserNameGitHubActionsBot && !people.ContainsKey(pr.Assignee.Login))
        {
            User assignee = _client.User.Get(pr.Assignee.Login).Result;
            people.TryAdd(assignee.Login, GetNameOrUserName(assignee));
        }
        if (pr.User.Login != UserNameGitHubActionsBot && !people.ContainsKey(pr.User.Login))
        {
            User creator = _client.User.Get(pr.User.Login).Result;
            people.TryAdd(creator.Login, GetNameOrUserName(creator));
        }
        foreach (string reviewerLogin in pr.RequestedReviewers.Where(r => r.Login != UserNameGitHubActionsBot && !people.ContainsKey(r.Login)).Select(r => r.Login))
        {
            User reviewer = _client.User.Get(reviewerLogin).Result;
            people.TryAdd(reviewer.Login, GetNameOrUserName(reviewer));
        }

        IReadOnlyList<PullRequestReview> reviews = _client.PullRequest.Review.GetAll(_org, _repo, pr.Number).Result;
        foreach (PullRequestReview review in reviews)
        {
            if (review.State == PullRequestReviewState.Approved && !people.ContainsKey(review.User.Login))
            {
                User reviewer = _client.User.Get(review.User.Login).Result;
                people.TryAdd(reviewer.Login, GetNameOrUserName(reviewer));
            }
        }
    }

    private string GetNameOrUserName(User user) => string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;

    private bool IsSkippable(string firstMessageLine, GitHubCommit commit, [NotNullWhen(returnValue: true)] out string? reason)
    {
        reason = null;

        // If the author is the Maestro bot, then the commit is skippable
        if (commit.Author.Login == UserNameMaestroBot)
        {
            reason = "author: maestrobot";
            return true;
        }

        foreach (string text in ForbiddenStrings)
        {
            // If the first line of the commit message contains any of the forbidden strings, then the commit is skippable
            if (firstMessageLine.Contains(text, StringComparison.InvariantCulture))
            {
                reason = $"Skip title text: {text}";
                return true;
            }
        }

        foreach (string pattern in ForbiddenPatterns)
        {
            // If the first line of the commit message matches any of the forbidden patterns, then the commit is skippable
            if (Regex.IsMatch(firstMessageLine, pattern))
            {
                reason = $"Skip title pattern: {pattern[..19]}";
                return true;
            }
        }

        // If all files in the commit are infra files, then the commit is skippable
        if (commit.Files.All(file => InfraExtensions.Any(ext => file.Filename.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase))))
        {
            reason = "All infra files";
            return true;
        }

        // If all files in the commit are test files, then the commit is skippable
        if (commit.Files.All(file => file.Filename.Contains("test", StringComparison.InvariantCultureIgnoreCase)))
        {
            reason = "All test files";
            return true;
        }

        return false;
    }

    private string RemoveTexts(string message)
    {
        foreach (string s in StringsToTrim)
        {
            message = message.Replace(s, string.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        foreach (string pattern in StringPatternsToTrim)
        {
            message = Regex.Replace(message, pattern, string.Empty, RegexOptions.IgnoreCase);
        }

        return message;
    }

    private string GetFirstLine(string txt) => txt.Split(NewLines, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First();
}