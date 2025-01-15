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

public partial class CommitCollector
{
    private const string UserNameMaestroBot = "dotnet-maestro[bot]";
    internal static readonly string UserNameGitHubActionsBot = "github-actions[bot]";
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
    private readonly Dictionary<string, User> _knownPeople;
    private readonly List<string> _errors;
    private string? _org;
    private string? _repo;

    private CommitCollector(GitHubClient client)
    {
        _client = client;
        _knownPeople = new Dictionary<string, User>();
        _errors = new List<string>();
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
                (PullRequest? pr, AuthorAndApprovers people) = GetPullRequestAuthorAndApprovers(firstMessageLine, ghCommit);
                string url = pr == null ? ghCommit.HtmlUrl : pr.HtmlUrl;
                table.WithRow($"[{firstMessageLine}]({url})", $"{people.Author} / {string.Join(", ", people.Approvers.Values)}", string.Empty /* Comments */,  string.Empty /* Validation status */);
            }
        }

        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
        ConsoleLog.WriteSuccess(table.ToString());
        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();

        var skippedTable = new MarkdownTableBuilder().WithHeader("Reason", "Title");

        ConsoleLog.WriteWarning("Commits that were skipped:");
        foreach ((string reason, string firstLine) in skipped)
        {
            skippedTable = skippedTable.WithRow(reason, firstLine);
        }
        ConsoleLog.WriteWarning(skippedTable.ToString());

        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();

        if (_errors.Any())
        {
            ConsoleLog.WriteError("People loading errors:");
            foreach (string error in _errors)
            {
                ConsoleLog.WriteError(error);
            }
        }
    }

    private (PullRequest?, AuthorAndApprovers) GetPullRequestAuthorAndApprovers(string firstLine, GitHubCommit commit)
    {
        AuthorAndApprovers people = new(commit.Commit.Author.Name);

        Match matchPrNumberInCommitTitle = MarkdownPrNumberRegex().Match(firstLine);
        int initialPrNumber;
        if (!matchPrNumberInCommitTitle.Success)
        {
            // Just in case, try find a prNumber in the full body
            matchPrNumberInCommitTitle = MarkdownPrNumberRegex().Match(commit.Commit.Message);
            if (!matchPrNumberInCommitTitle.Success)
            {
                _errors.Add($"{commit.Sha[..8]} - {firstLine} - No PR number found in the commit title.");
                return (null, people);
            }
        }

        initialPrNumber = int.Parse(matchPrNumberInCommitTitle.Groups["prNumber"].Value);
        if (!TryGetPR(initialPrNumber, out PullRequest? pr))
        {
            return (null, people);
        }
        AddPeople(people, pr);

        if (people.Author == UserNameGitHubActionsBot)
        {
            // The initial PR is a backport PR
            Match matchOriginalPrNumberInBackportBody = MarkdownPrNumberRegex().Match(pr.Body);
            if (!matchOriginalPrNumberInBackportBody.Success)
            {
                _errors.Add($"{commit.Commit.Sha[..8]} - {firstLine} - Did not find 'Backport of' text in PR body.");
                return (pr, people);
            }

            int actualPrNumber = int.Parse(matchOriginalPrNumberInBackportBody.Groups["prNumber"].Value);
            if (!TryGetPR(actualPrNumber, out PullRequest? actualPr))
            {
                return (pr, people);
            }
            AddPeople(people, actualPr);

            // Only one more level check, in case it's a backport of another backport
            if (actualPr.User.Login == UserNameGitHubActionsBot)
            {
                Match matchfirstPrLink = MarkdownPrNumberRegex().Match(actualPr.Body);
                if (!matchfirstPrLink.Success)
                {
                    _errors.Add($"{commit.Commit.Sha[..8]} - {firstLine} - Could not find a link to the second backport PR.");
                    return (actualPr, people);
                }

                int secondBackportPrNumber = int.Parse(matchfirstPrLink.Groups["prNumber"].Value);
                if (!TryGetPR(secondBackportPrNumber, out PullRequest? SecondPr))
                {
                    return (actualPr, people);
                }
                AddPeople(people, SecondPr);
                pr = SecondPr;
            }
        }

        return (pr, people);
    }

    private bool TryGetPR(int prNumber, [NotNullWhen(returnValue: true)] out PullRequest? pr)
    {
        pr = _client.PullRequest.Get(_org, _repo, prNumber).Result;

        if (pr == null)
        {
            _errors.Add($"Could not retrieve PR for pr number {prNumber}.");
        }

        return pr != null;
    }

    private void AddPeople(AuthorAndApprovers people, PullRequest pr)
    {
        if (pr.User.Login != UserNameGitHubActionsBot)
        {
            User creator = GetCachedUser(pr.User.Login);
            people.Update(creator);
        }

        if (pr.Assignee != null && pr.Assignee.Login != UserNameGitHubActionsBot)
        {
            User assignee = GetCachedUser(pr.Assignee.Login);
            people.Update(assignee);
        }

        foreach (string reviewerLogin in pr.RequestedReviewers.Where(r => r.Login != UserNameGitHubActionsBot && !people.Approvers.ContainsKey(r.Login)).Select(r => r.Login))
        {
            User reviewer = GetCachedUser(reviewerLogin);
            people.Update(reviewer);
        }

        IReadOnlyList<PullRequestReview> reviews = _client.PullRequest.Review.GetAll(_org, _repo, pr.Number).Result;
        foreach (PullRequestReview review in reviews)
        {
            if (review.State == PullRequestReviewState.Approved && !people.Approvers.ContainsKey(review.User.Login))
            {
                User reviewer = GetCachedUser(review.User.Login);
                people.Update(reviewer);
            }
        }
    }

    private User GetCachedUser(string login)
    {
        if (_knownPeople.TryGetValue(login, out User? value))
        {
            return value;
        }

        User user = _client.User.Get(login).Result;
        _knownPeople.Add(login, user);
        return user;
    }

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

    [GeneratedRegex(@"\(\#(?<prNumber>\d+)\)")]
    private static partial Regex MarkdownPrNumberRegex();
}

internal class AuthorAndApprovers
{
    public string Author { get; set; }
    public Dictionary<string, string> Approvers { get; } // alias -> name
    public AuthorAndApprovers(string commitCreator)
    {
        Author = commitCreator;
        Approvers = new Dictionary<string, string>();
    }
    public void Update(User user)
    {
        string name = GetNameOrUserName(user);
        if (Author == string.Empty || Author == CommitCollector.UserNameGitHubActionsBot)
        {
            Author = name;
        }
        if (!Approvers.ContainsKey(user.Login))
        {
            Approvers.Add(user.Login, name);
        }
    }
    private string GetNameOrUserName(User user) => string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;

}