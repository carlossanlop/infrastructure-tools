using InfrastructureTools.Shared;
using InfrastructureTools.Connectors.GitHub;
using Octokit;
using InfrastructureTools.MarkdownTable;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

namespace InfrastructureTools.CommitCollect;

public class CommitCollector
{
    private const string UserNameMaestroBot = "dotnet-maestro[bot]";
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

    public void Run(string org, string repo)
    {
        Console.Write("Enter the ID of the PR with the manual merge from staging to base: ");
        string? strId = Console.ReadLine();
        ArgumentException.ThrowIfNullOrEmpty(strId);
        int prId = int.Parse(strId);

        IReadOnlyList<PullRequestCommit> commits = _client.PullRequest.Commits(org, repo, prId).Result;

        List<(string, string)> skipped = new();

        var table = new MarkdownTableBuilder().WithHeader("PR", "Author/Approvers", "Comments", "Validation status");

        foreach (PullRequestCommit commit in commits)
        {
            string firstMessageLine = commit.Commit.Message.Split(NewLines, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First();
            GitHubCommit ghCommit = _client.Repository.Commit.Get(org, repo, commit.Sha).Result;
            if (IsSkippable(firstMessageLine, ghCommit, out string? reason))
            {
                skipped.Add((reason, firstMessageLine));
            }
            else
            {
                firstMessageLine = RemoveTexts(firstMessageLine);
                table.WithRow($"[{firstMessageLine}]({commit.Url})", commit.Author.Login, string.Empty /* Comments */,  string.Empty /* Validation status */);
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
}