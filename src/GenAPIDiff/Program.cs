using System.CommandLine;
using System.CommandLine.Binding;

RootCommand rootCommand = new("GenAPIDiff is a command line tool to check API changes between a two sets of .NET assemblies.");

Option<FileSystemInfo> optionOldSet = new(["-os", "--OldSet"], "A path to an assembly or a directory containing assemblies that will be used as the baseline for the comparison.");
Option<FileSystemInfo> optionNewSet = new(["-ns", "--NewSet"], "A path to an assembly or a directory containing assemblies that will be compared to the baseline.");
Option<string> optionOldSetName = new(["-osn", "--OldSetName"], "A name for the old set in the output. The default is the file or directory name will be used.");
Option<string> optionNewSetName = new(["-nsn", "--NewSetName"], "A name for the new set in the output. The default is the file or directory name will be used.");

rootCommand.AddOption(optionOldSet);
rootCommand.AddOption(optionNewSet);
rootCommand.AddOption(optionOldSetName);
rootCommand.AddOption(optionNewSetName);

//rootCommand.AddOption(new Option<bool>(["-u", "--Unchanged"], () => false, "Include members, types, and namespaces that were unchanged. Default is false."));
//rootCommand.AddOption(new Option<bool>(["-r", "--Removed"], () => true, "Include members, types, and namespaces that were removed. Default is true."));
//rootCommand.AddOption(new Option<bool>(["-a", "--Added"], () => true, "Include members, types, and namespaces that were added. Default is true."));
//rootCommand.AddOption(new Option<bool>(["-c", "--Changed"], () => true, "Include members, types, and namespaces that were changed. Default is true."));
//rootCommand.AddOption(new Option<bool>(["-itc", "--IncludeTableOfContents"], () => true, "Include table of contents as part of the diff. Default is true."));
//rootCommand.AddOption(new Option<bool>(["-da", "--DiffAttributes"], () => true, "Enables diffing of the attributes as well, by default all attributes are ignored. Default is true."));
//rootCommand.AddOption(new Option<bool>(["-adm", "--AlwaysDiffMembers"], () => true, "If an entire class is added or removed, decide to show all its members or not. Default is true (show members)."));
//rootCommand.AddOption(new Option<bool>(["-hbm", "--HighlightBaseMembers"], () => true, "Decide to highlight members that are interface implementations or overrides of a base member. Default is true."));
//rootCommand.AddOption(new Option<bool>(["-cfn", "--CreateFilePerNamespace"], () => true, "Decide to Create files per namespace. Default is true."));

rootCommand.SetHandler(ProcessArguments, new GenApiDiffConfigurationBinder(optionOldSet, optionNewSet, optionOldSetName, optionNewSetName));

await rootCommand.InvokeAsync(args);

static void ProcessArguments(GenApiDiffConfiguration config)
{
    Console.WriteLine("Hello world");
    Console.WriteLine(config.OldSetName);
}

internal class GenApiDiffConfiguration
{
    public required FileSystemInfo OldSet { get; set; }
    public required FileSystemInfo NewSet { get; set; }
    public string? OldSetName { get; set; }
    public string? NewSetName { get; set; }
}

internal class GenApiDiffConfigurationBinder(
    Option<FileSystemInfo> optionOldSet, Option<FileSystemInfo> optionNewSet, Option<string> optionOldSetName, Option<string> optionNewSetName)
    : BinderBase<GenApiDiffConfiguration>
{
    protected override GenApiDiffConfiguration GetBoundValue(BindingContext bindingContext) =>
        new GenApiDiffConfiguration
        {
            OldSet = bindingContext.ParseResult.GetValueForOption(optionOldSet) ?? throw new NullReferenceException("OldSet"),
            NewSet = bindingContext.ParseResult.GetValueForOption(optionNewSet) ?? throw new NullReferenceException("NewSet"),
            OldSetName = bindingContext.ParseResult.GetValueForOption(optionOldSetName),
            NewSetName = bindingContext.ParseResult.GetValueForOption(optionNewSetName)
        };
}