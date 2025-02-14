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
using System.Text.Json;

namespace InfrastructureTools.CommitCollector;

public partial class CommitCollector
{

    public static readonly string DefaultConfigJsonFileName = "infrastructure-tools-settings.json";
    public static string DefaultConfigJsonFilePath = Path.Join(Path.GetTempPath(), DefaultConfigJsonFileName);
    internal static readonly string UserNameGitHubActionsBot = "github-actions[bot]";
    private const string UserNameMaestroBot = "dotnet-maestro[bot]";
    private readonly string[] InfraExtensions = ["CMakeLists.txt", ".cmake", ".config", ".csproj", ".editorconfig", ".gitignore", ".ilproj", ".inc", ".json", ".md", ".pp", ".proj", ".props", ".ps1", ".ruleset", ".S", ".sln", ".targets", ".txt", ".xml", ".yml"];
    private readonly string[] ForbiddenStrings = [
        "Update dependencies from ",
        "Merge branch "
    ];
    private readonly string[] ForbiddenPatterns = [
        @"Merge pull request (dotnet)?\#\d+ from "
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
    private static readonly char[] NewLineChar = ['\n'];

    private readonly Dictionary<string, User> _knownPeople;
    private readonly List<string> _errors;
    private readonly string _org;
    private readonly string _repo;

    public static Lazy<string> SerializedGitHubOptions => new Lazy<string>(() => JsonSerializer.Serialize(value: new GitHubOptions(), options: new JsonSerializerOptions() { WriteIndented = true }));

    public GitHubClient Client { get; }

    private CommitCollector(GitHubClient client, string org, string repo)
    {
        Client = client;
        _org = org;
        _repo = repo;
        _knownPeople = new Dictionary<string, User>();
        _errors = new List<string>();
    }

    public static async Task<CommitCollector> CreateAsync(string configFilePath, string org, string repo, bool askForOptions = true)
    {
        string? optionsFilePath;

        if (File.Exists(configFilePath))
        {
            optionsFilePath = configFilePath;
        }
        else if (File.Exists(DefaultConfigJsonFilePath))
        {
            optionsFilePath = DefaultConfigJsonFilePath;
        }
        else
        {
            string noFilesFoundMessage = $"No GitHub options file found in the specified config path '{configFilePath}' or in the default config path '{DefaultConfigJsonFilePath}'.";
            if (askForOptions)
            {
                Console.WriteLine(noFilesFoundMessage);
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
                throw new FileNotFoundException(noFilesFoundMessage);
            }
        }

        GitHubClient client = await GitHubAuthenticator.GetClientAsync(optionsFilePath);
        if (optionsFilePath != DefaultConfigJsonFilePath)
        {
            File.Copy(optionsFilePath, DefaultConfigJsonFilePath);
        }

        return new CommitCollector(client, org, repo);
    }

    public void Run(int prNumber)
    {
        IReadOnlyList<PullRequestCommit> prCommits = Client.PullRequest.Commits(_org, _repo, prNumber).Result;

        List<(PullRequestCommit, GitHubCommit)> included = new();
        List<(PullRequestCommit, string)> skipped = new();

        foreach (PullRequestCommit prCommit in prCommits)
        {
            ProcessPullRequestCommit(prCommit, included, skipped);
        }

        PrintIncludedTable(included);
        PrintSkippedTable(skipped);
        PrintErrors();
    }

    public void ProcessPullRequestCommit(PullRequestCommit prCommit, List<(PullRequestCommit, GitHubCommit)> included, List<(PullRequestCommit, string)> skipped)
    {
        GitHubCommit ghCommit = Client.Repository.Commit.Get(_org, _repo, prCommit.Sha).Result;
        if (IsSkippable(prCommit, ghCommit, out string? reason))
        {
            skipped.Add((prCommit, reason));
        }
        else
        {
            included.Add((prCommit, ghCommit));
        }
    }

    public (PullRequest?, AuthorAndApprovers) GetPullRequestAuthorAndApprovers(PullRequestCommit prCommit, GitHubCommit gcCommit)
    {
        AuthorAndApprovers people = new(gcCommit.Commit.Author.Name);

        ReadOnlySpan<char> firstLine = GetFirstLine(prCommit.Commit.Message);
        Match matchPrNumberInCommitTitle = MarkdownPrNumberRegex().Match(prCommit.Commit.Message);
        int initialPrNumber;
        if (!matchPrNumberInCommitTitle.Success)
        {
            // Just in case, try find a prNumber in the full body
            matchPrNumberInCommitTitle = MarkdownPrNumberRegex().Match(gcCommit.Commit.Message);
            if (!matchPrNumberInCommitTitle.Success)
            {
                _errors.Add($"{gcCommit.Sha[..8]} - {firstLine} - No PR number found in the commit title.");
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
                _errors.Add($"{gcCommit.Commit.Sha[..8]} - {firstLine} - Did not find 'Backport of' text in PR body.");
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
                    _errors.Add($"{gcCommit.Commit.Sha[..8]} - {firstLine} - Could not find a link to the second backport PR.");
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

    private void PrintIncludedTable(List<(PullRequestCommit, GitHubCommit)> included)
    {
        var table = new MarkdownTableBuilder().WithHeader("PR", "Author/Approvers", "Comments", "Validation status");
        foreach ((PullRequestCommit prCommit, GitHubCommit ghCommit) in included)
        {
            (PullRequest? pr, AuthorAndApprovers people) = GetPullRequestAuthorAndApprovers(prCommit, ghCommit);
            string url = pr == null ? ghCommit.HtmlUrl : pr.HtmlUrl;
            ReadOnlySpan<char> firstLine = GetFirstLine(prCommit.Commit.Message);
            string cleanedLine = RemoveUndesiredTexts(firstLine);
            table = table.WithRow($"[{cleanedLine}]({url})",
                          $"{people.Author} / {string.Join(", ", people.Approvers.Values)}",
                          string.Empty /* Comments */,
                          string.Empty /* Validation status */);
        }

        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
        ConsoleLog.WriteSuccess(table.ToString());
        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
    }

    private void PrintSkippedTable(List<(PullRequestCommit, string)> skipped)
    {
        var table = new MarkdownTableBuilder().WithHeader("Reason", "Title");
        foreach ((PullRequestCommit prCommit, string reason) in skipped)
        {
            ReadOnlySpan<char> line = GetFirstLine(prCommit.Commit.Message);
            ReadOnlySpan<char> firstLine = GetFirstLine(prCommit.Commit.Message);
            string cleanedLine = RemoveUndesiredTexts(firstLine);
            table = table.WithRow(reason, $"{cleanedLine}");
        }

        ConsoleLog.WriteWarning("Commits that were skipped:");
        ConsoleLog.WriteWarning(table.ToString());

        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
    }

    private void PrintErrors()
    {
        if (_errors.Any())
        {
            ConsoleLog.WriteError("People loading errors:");
            foreach (string error in _errors)
            {
                ConsoleLog.WriteError(error);
            }
        }
    }

    private bool TryGetPR(int prNumber, [NotNullWhen(returnValue: true)] out PullRequest? pr)
    {
        pr = Client.PullRequest.Get(_org, _repo, prNumber).Result;

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

        IReadOnlyList<PullRequestReview> reviews = Client.PullRequest.Review.GetAll(_org, _repo, pr.Number).Result;
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

        User user = Client.User.Get(login).Result;
        _knownPeople.Add(login, user);
        return user;
    }

    private bool IsSkippable(PullRequestCommit prCommit, GitHubCommit ghCommit, [NotNullWhen(returnValue: true)] out string? reason)
    {
        reason = null;

        ReadOnlySpan<char> firstMessageLine = GetFirstLine(prCommit.Commit.Message);

        // If the author is the Maestro bot, then the commit is skippable
        if (ghCommit.Author.Login == UserNameMaestroBot)
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
        if (ghCommit.Files.All(file => InfraExtensions.Any(ext => file.Filename.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase))))
        {
            reason = "All infra files";
            return true;
        }

        // If all files in the commit are test files, then the commit is skippable
        if (ghCommit.Files.All(file => file.Filename.Contains("test", StringComparison.InvariantCultureIgnoreCase)))
        {
            reason = "All test files";
            return true;
        }

        return false;
    }

    private string RemoveUndesiredTexts(ReadOnlySpan<char> message)
    {
        string result = message.ToString();

        foreach (string s in StringsToTrim)
        {
            result = result.Replace(s, string.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        foreach (string pattern in StringPatternsToTrim)
        {
            result = Regex.Replace(result, pattern, string.Empty, RegexOptions.IgnoreCase);
        }

        return result;
    }

    private ReadOnlySpan<char> GetFirstLine(string txt)
    {
        int index = txt.IndexOfAny(NewLineChar);
        if (index == -1)
        {
            index = txt.Length;
        }

        return txt.AsSpan(0, index);
    }

    [GeneratedRegex(@"\(\#(?<prNumber>\d+)\)")]
    private static partial Regex MarkdownPrNumberRegex();
}

public class AuthorAndApprovers
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
        if (Author != name && !Approvers.ContainsKey(user.Login))
        {
            // Only add this user to approvers if it is not the author
            Approvers.Add(user.Login, name);
        }
    }
    private string GetNameOrUserName(User user) => string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;

}