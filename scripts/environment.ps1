$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$buildRoot = Join-Path $projectRoot '.build'
New-Item -ItemType Directory -Force $buildRoot | Out-Null
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (!$dotnet -and $env:DOTNET_ROOT) { $dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe' }
if (!$dotnet -or !(Test-Path $dotnet)) { throw 'Bitte .NET SDK 10 installieren und dotnet zum PATH hinzufügen.' }
$env:DOTNET_ROOT = Split-Path $dotnet
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
$wix = Join-Path $buildRoot 'tools/wix.exe'
function Initialize-Wix {
    if (!(Test-Path $wix)) {
        & $dotnet tool install wix --version 4.0.6 --tool-path "$buildRoot/tools"
        if ($LASTEXITCODE -ne 0) { throw 'WiX-Installation fehlgeschlagen.' }
    }
}
