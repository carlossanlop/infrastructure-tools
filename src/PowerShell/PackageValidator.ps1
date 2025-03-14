# Script that takes the folder containing nupkgs and/or dlls then runs various validation checks on them.

Param (
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory
)

$NugetExe = $(where.exe nuget.exe)
$SignToolExe = (where.exe signtool.exe)
$SnExe = (where.exe sn.exe)
$SymChkExe = "C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\symchk.exe"
VerifyPathOrExit $SymChkExe

Function Write-Color {
    Param (
        [ValidateNotNullOrEmpty()]
        [string] $newColor
    )

    $oldColor = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $newColor

    If ($args) {
        Write-Output $args
    }
    Else {
        $input | Write-Output
    }

    $host.UI.RawUI.ForegroundColor = $oldColor
}

Function VerifyPathOrExit {
    Param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $path
    )

    If (-Not (Test-Path -Path $path)) {
        Write-Error "The path '$path' does not exist." -ErrorAction Stop
    }
}

function ValidatePackage {
    Param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $packagePath
    )
    VerifyPathOrExit $packagePath

    Write-Color green "Nuget verify"
    & $NugetExe verify -Signature -All $packagePath
    Write-Color green "---"
}

function ValidateDll {
    Param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $dllPath
    )

    VerifyPathOrExit $dllPath

    Write-Color green "SignTool verify"
    & $SignToolExe verify /pa $dllPath
    Write-Color green "---"
    Write-Color green "Sn verify"
    & $SnExe /vf $dllPath
    Write-Color green "---"
    Write-Color green "SymChk verify"
    & $SymChkExe /s SRV*https://msdl.microsoft.com/download/symbols $dllPath
    Write-Color green "---"
}

Get-ChildItem -Path $PackageDirectory -Filter *.nupkg -Recurse -File | ForEach-Object { ValidatePackage $_.FullName }

Get-ChildItem -Path $PackageDirectory -Filter *.dll   -Recurse -File | ForEach-Object { ValidateDll $_.FullName }