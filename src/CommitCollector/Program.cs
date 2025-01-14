namespace InfrastructureTools.CommitCollect;

public static class Program
{
    private static async Task Main(string[] args)
    {
        if (args != null && args.Length == 1 && args[0] == "-d")
        {
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine($"Attach to {Environment.ProcessId}");
                Thread.Sleep(1000);
            }
            Console.WriteLine("Attached");
            System.Diagnostics.Debugger.Break();
        }

        CommitCollector collector = await CommitCollector.CreateAsync();
        collector.Run("dotnet", "runtime");
    }
}