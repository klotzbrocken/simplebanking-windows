param([switch]$Test, [string]$OutputDirectory = "$PSScriptRoot/app", [string]$YaxiKeysFile)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/build.ps1" -Native -Test:$Test -YaxiKeysFile $YaxiKeysFile
. "$PSScriptRoot/scripts/environment.ps1"
$properties = @()
if ($YaxiKeysFile) { $properties += "-p:YaxiKeysFile=$((Resolve-Path $YaxiKeysFile).Path)" }
& $dotnet publish "$PSScriptRoot/src/SimpleBanking.Windows" -c Release -r win-x64 --self-contained true -o $OutputDirectory @properties
if ($LASTEXITCODE -ne 0) { throw 'Publish fehlgeschlagen.' }
Copy-Item "$PSScriptRoot/native/vendor/*-LICENSE.txt" $OutputDirectory -Force
Copy-Item "$PSScriptRoot/reference/MAC-LICENSE.txt" $OutputDirectory -Force
Copy-Item "$PSScriptRoot/reference/licenses/*.txt" $OutputDirectory -Force
