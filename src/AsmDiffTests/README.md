# ASM Diff Tests

This solution contains two projects that can be compared using the dotnet/arcade tool Microsoft.DotNet.AsmDiff.

Add, remove or change APIs from both Code.cs files inside the Before and After projects to see how the AsmDiff tool shows them in the diff output.

## Usage

The following instructions assume that:
- You use PowerShell.
- All your repos are stored in the path indicated by the `$ENV:SOURCEREPOS` environment variable.

Command:

```powershell
$AsmDiffExe="$ENV:SOURCEREPOS\arcade\artifacts\bin\Microsoft.DotNet.AsmDiff\Debug\net8.0\Microsoft.DotNet.AsmDiff.exe"

$TableOfContents="$ENV:SOURCEREPOS\infrastructure-tools\src\AsmDiffTests\TableOfContents.md"

$Before="$ENV:SOURCEREPOS\infrastructure-tools\src\AsmDiffTests\Before\bin\Debug\net8.0"

$After="$ENV:SOURCEREPOS\infrastructure-tools\src\AsmDiffTests\After\bin\Debug\net8.0"

& $AsmDiffExe -r -a -c -itc -cfn -adm -hbm -da -w markdown -o $TableOfContents -os $Before -ns $After

code "$ENV:SOURCEREPOS\infrastructure-tools\src\AsmDiffTests\TableOfContents_AsmDiffTests.md"
```