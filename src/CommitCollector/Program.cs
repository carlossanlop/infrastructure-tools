using InfrastructureTools.Shared;
using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureTools.CommitCollector;

public static class Program
{
    public static async Task Main(string[] args)
    {
        RootCommand rootCommand = new("commit-collector");


        Option<FileInfo> optionConfig = new(["-config", "-c"], () => new FileInfo(CommitCollector.DefaultConfigJsonFilePath), $"The GitHub configuration file to use. If the default file is not found in {CommitCollector.DefaultConfigJsonFilePath}, you'll be prompted to specify a path. The json file must have this format:{Environment.NewLine}{CommitCollector.SerializedGitHubOptions.Value}{Environment.NewLine}");
        Option<int> optionPr = new("-pr", "The PR number to collect commits from.")
        {
            IsRequired = true
        };
        Option<string> optionOrg = new(["-org", "-o"], () => "dotnet", "The organization to collect commits from.");
        Option<string> optionRepo = new(["-repo", "-r"], () => "runtime", "The repo to collect commits from.");
        Option<bool> optionDebug = new(["--debug", "-d"], () => false, "Break execution at the beginning to attach a debugger.");

        rootCommand.Add(optionPr);
        rootCommand.Add(optionOrg);
        rootCommand.Add(optionRepo);
        rootCommand.Add(optionDebug);
        rootCommand.Add(optionConfig);

        rootCommand.SetHandler(async (pr, org, repo, debug, config) =>
        {
            ConsoleLog.WriteInfo($"Pull Request number: {pr}");
            ConsoleLog.WriteInfo($"Org: {org}");
            ConsoleLog.WriteInfo($"Repo: {repo}");
            ConsoleLog.WriteInfo($"Config file: {config}");
            ConsoleLog.WriteInfo($"Debug: {debug}");

            if (debug)
            {
                while (!System.Diagnostics.Debugger.IsAttached)
                {
                    ConsoleLog.WriteWarning($"Attach to {Environment.ProcessId}...");
                    Thread.Sleep(1000);
                }
                ConsoleLog.WriteSuccess("Attached!");
                System.Diagnostics.Debugger.Break();
            }

            CommitCollector collector = await CommitCollector.CreateAsync(config.FullName, org, repo);
            collector.Run(pr);

        },
        optionPr, optionOrg, optionRepo, optionDebug, optionConfig);

        await rootCommand.InvokeAsync(args);
    }
}