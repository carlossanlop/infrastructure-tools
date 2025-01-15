using InfrastructureTools.Shared;
using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureTools.CommitCollect;

public static class Program
{
    public static async Task Main(string[] args)
    {
        RootCommand rootCommand = new("commit-collector");

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

        rootCommand.SetHandler(async (pr, org, repo, debug) =>
        {
            ConsoleLog.WriteInfo($"Pull Request number: {pr}");
            ConsoleLog.WriteInfo($"Org: {org}");
            ConsoleLog.WriteInfo($"Repo: {repo}");
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

            CommitCollector collector = await CommitCollector.CreateAsync();
            collector.Run(org, repo, pr);

        },
        optionPr, optionOrg, optionRepo, optionDebug);

        await rootCommand.InvokeAsync(args);
    }
}