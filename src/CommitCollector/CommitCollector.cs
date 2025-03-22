using InfrastructureTools.Connectors.GitHub;
using InfrastructureTools.MarkdownTable;
using InfrastructureTools.Shared;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace InfrastructureTools.CommitCollector;

public partial class CommitCollector
{
    public static readonly string DefaultConfigJsonFileName = "infrastructure-tools-settings.json";
    public static string DefaultConfigJsonFilePath = Path.Join(Path.GetTempPath(), DefaultConfigJsonFileName);
    internal static readonly string UserNameGitHubActionsBot = "github-actions[bot]";
    private const string UserNameMaestroBot = "dotnet-maestro[bot]";
    private readonly string[] InfraExtensions = ["CMakeLists.txt", ".cmake", ".config", ".csproj", ".editorconfig", ".gitignore", ".ilproj", ".inc", ".json", ".md", ".pp", ".proj", ".props", ".ps1", ".ruleset", ".S", ".sh", ".sln", ".targets", ".txt", ".xml", ".yml"];
    private readonly string[] ForbiddenStrings = [
        "Update dependencies from ",
        "Merge branch ",
        "Merge remote-tracking branch "
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
    private readonly string _org;
    private readonly string _repo;

    public List<string> Errors { get; }

    public static Lazy<string> SerializedGitHubOptions => new Lazy<string>(() => JsonSerializer.Serialize(value: new GitHubOptions(), options: new JsonSerializerOptions() { WriteIndented = true }));

    public GitHubClient Client { get; }

    private CommitCollector(GitHubClient client, string org, string repo)
    {
        Client = client;
        _org = org;
        _repo = repo;
        _knownPeople = new Dictionary<string, User>();
        Errors = new List<string>();
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

        List<PullRequestInformation> prInfos = new();
        List<(PullRequestCommit, string)> skipped = new();

        foreach (PullRequestCommit prCommit in prCommits)
        {
            ProcessPullRequestCommit(prCommit, prInfos, skipped);
        }

        PrintIncludedTable(prInfos);
        PrintSkippedTable(skipped);
        PrintAliases(prInfos);
        PrintErrors();
    }

    public void ProcessPullRequestCommit(PullRequestCommit prCommit, List<PullRequestInformation> prInfos, List<(PullRequestCommit, string)> skipped)
    {
        GitHubCommit ghCommit = Client.Repository.Commit.Get(_org, _repo, prCommit.Sha).Result;
        if (IsSkippable(prCommit, ghCommit, out string? reason))
        {
            skipped.Add((prCommit, reason));
        }
        else
        {
            (PullRequest? pr, PullRequestPeople people) = GetPullRequestAuthorAndApprovers(prCommit, ghCommit);
            prInfos.Add(new PullRequestInformation(prCommit, ghCommit, pr, people));
        }
    }

    public MarkdownTableBuilder GetIncludedTable(List<PullRequestInformation> prInfos)
    {
        MarkdownTableBuilder table = new MarkdownTableBuilder()
            .WithHeader("PR", "Author/Approvers", "Comments", "Validation status");

        foreach (PullRequestInformation info in prInfos)
        {
            string url = info.PR == null ? info.GHCommit.HtmlUrl : info.PR.HtmlUrl;
            string firstLine = GetFirstLine(info.PRCommit.Commit.Message);
            string cleanedLine = RemoveUndesiredTexts(firstLine);
            // Sort them by full name, which is the dict value
            string approvers = string.Join(", ", info.People.Approvers.Values.Order());
            table = table.WithRow($"[{cleanedLine}]({url})",
                          $"{info.People.Author} / {approvers}",
                          string.Empty /* Comments */,
                          string.Empty /* Validation status */);
        }

        return table;
    }

    public MarkdownTableBuilder GetSkippedTable(List<(PullRequestCommit, string)> skipped)
    {
        MarkdownTableBuilder table = new MarkdownTableBuilder()
            .WithHeader("Reason", "Title");

        foreach ((PullRequestCommit prCommit, string reason) in skipped)
        {
            string line = GetFirstLine(prCommit.Commit.Message);
            string firstLine = GetFirstLine(prCommit.Commit.Message);
            string cleanedLine = RemoveUndesiredTexts(firstLine);
            table = table.WithRow(reason, $"{cleanedLine}");
        }

        return table;
    }

    private bool TryGetPullRequestNumberUsingPullRequestCommit(PullRequestCommit prCommit, string firstLine, out int initialPrNumber)
    {
        IReadOnlyList<CommitPullRequest> pullRequests = Client.Repository.Commit.PullRequests(_org, _repo, prCommit.Sha).Result;
        if (pullRequests.Any())
        {
            CommitPullRequest? commitPullRequest = pullRequests.FirstOrDefault(p => p.Title.Contains(firstLine));
            if (commitPullRequest != null)
            {
                initialPrNumber = commitPullRequest.Number;
                return true;
            }
        }

        initialPrNumber = -1;
        return false;
    }

    private bool TryGetPullRequestNumberFromCommitMessage(PullRequestCommit prCommit, GitHubCommit gcCommit, string firstLine, out int initialPrNumber)
    {
        Match matchPrNumberInPullRequestCommitTitle = MarkdownPrNumberRegex().Match(prCommit.Commit.Message);

        if (matchPrNumberInPullRequestCommitTitle.Success)
        {
            initialPrNumber = int.Parse(matchPrNumberInPullRequestCommitTitle.Groups["prNumber"].Value);
            return true;
        }

        Match matchPrNumberInGitHubCommitTitle = MarkdownPrNumberRegex().Match(gcCommit.Commit.Message);
        if (matchPrNumberInGitHubCommitTitle.Success)
        {
            initialPrNumber = int.Parse(matchPrNumberInPullRequestCommitTitle.Groups["prNumber"].Value);
            return true;
        }

        Errors.Add($"{gcCommit.Sha[..7]} - {firstLine} - No PR number found in the commit title.");
        initialPrNumber = -1;
        return false;

    }

    public (PullRequest?, PullRequestPeople) GetPullRequestAuthorAndApprovers(PullRequestCommit prCommit, GitHubCommit ghCommit)
    {
        PullRequestPeople people = new(ghCommit.Commit.Author.Name);
        string firstLine = GetFirstLine(prCommit.Commit.Message);

        if ((!TryGetPullRequestNumberUsingPullRequestCommit(prCommit, firstLine, out int initialPrNumber) &&
             !TryGetPullRequestNumberFromCommitMessage(prCommit, ghCommit, firstLine, out initialPrNumber)) ||
            !TryGetPR(initialPrNumber, out PullRequest? pr))
        {
            return (null, people);
        }

        AddPeople(people, pr);

        if (people.Author == UserNameGitHubActionsBot)
        {
            // The initial PR is a backport PR
            Match matchOriginalPrNumberInBackportBody = MarkdownBackportOfPrNumberRegex().Match(pr.Body);
            if (!matchOriginalPrNumberInBackportBody.Success)
            {
                matchOriginalPrNumberInBackportBody = MarkdownPrNumberRegex().Match(pr.Body);
                if (!matchOriginalPrNumberInBackportBody.Success)
                {
                    Errors.Add($"{ghCommit.Sha[..7]} - {firstLine} - Did not find 'Backport of' text in PR body.");
                    return (pr, people);
                }
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
                    Errors.Add($"{ghCommit.Commit.Sha[..7]} - {firstLine} - Could not find a link to the second backport PR.");
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

        Debug.Assert(people.Author != UserNameGitHubActionsBot);
        return (pr, people);
    }

    private void PrintIncludedTable(List<PullRequestInformation> includedDatas)
    {
        MarkdownTableBuilder table = GetIncludedTable(includedDatas);

        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
        ConsoleLog.WriteSuccess(table.ToString());
        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
    }

    private void PrintSkippedTable(List<(PullRequestCommit, string)> skipped)
    {
        MarkdownTableBuilder table = GetSkippedTable(skipped);

        ConsoleLog.WriteWarning("Commits that were skipped:");
        ConsoleLog.WriteWarning(table.ToString());

        Console.WriteLine();
        ConsoleLog.WriteWarning("-----");
        Console.WriteLine();
    }

    private void PrintAliases(List<PullRequestInformation> prInfos)
    {
        SortedSet<string> approvers = new();
        SortedSet<string> authors = new();

        foreach (PullRequestInformation info in prInfos)
        {
            if (!authors.Contains(info.People.Author))
            {
                authors.Add(info.People.Author);
            }
            foreach (string name in info.People.Approvers.Values)
            {
                // Only add to the approvers line if it's not already among authors
                if (!authors.Contains(name) && !approvers.Contains(name))
                {
                    approvers.Add(name);
                }
            }
        }

        Console.WriteLine("-----");
        Console.WriteLine("Authors and approvers:");
        ConsoleLog.WriteSuccess(string.Join(';', authors));
        ConsoleLog.WriteSuccess(string.Join(';', approvers));
        Console.WriteLine("-----");
    }

    private void PrintErrors()
    {
        if (Errors.Any())
        {
            ConsoleLog.WriteError("People loading errors:");
            foreach (string error in Errors)
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
            Errors.Add($"Could not retrieve PR for pr number {prNumber}.");
        }

        return pr != null;
    }

    private void AddPeople(PullRequestPeople people, PullRequest pr)
    {
        if (pr.User.Login != UserNameGitHubActionsBot)
        {
            User creator = GetCachedUser(pr.User.Login);
            people.TryAddAuthor(creator);
        }

        if (pr.Assignee != null && pr.Assignee.Login != UserNameGitHubActionsBot)
        {
            User assignee = GetCachedUser(pr.Assignee.Login);
            if (string.IsNullOrEmpty(people.Author))
            {
                people.TryAddAuthor(assignee);
            }
            else
            {
                people.TryAddApprover(assignee);
            }
        }

        // Do not look among pr.RequestReviewers because that will return people who were asked
        // to review but did not provide a review at all.
        IReadOnlyList<PullRequestReview> reviews = Client.PullRequest.Review.GetAll(_org, _repo, pr.Number).Result;
        foreach (PullRequestReview review in reviews)
        {
            if (review.State == PullRequestReviewState.Approved && !people.Approvers.ContainsKey(review.User.Login))
            {
                User reviewer = GetCachedUser(review.User.Login);
                people.TryAddApprover(reviewer);
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

        string firstMessageLine = GetFirstLine(prCommit.Commit.Message);

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

    private string GetFirstLine(string txt)
    {
        int index = txt.IndexOfAny(NewLineChar);
        if (index == -1)
        {
            index = txt.Length;
        }

        return txt.Substring(0, index);
    }

    [GeneratedRegex(@"Backport of #(?<prNumber>\d+)")]
    private static partial Regex MarkdownBackportOfPrNumberRegex();

    [GeneratedRegex(@"\(\#(?<prNumber>\d+)\)")]
    private static partial Regex MarkdownPrNumberRegex();
}