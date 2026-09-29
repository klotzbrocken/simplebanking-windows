param([switch]$Native, [switch]$Test, [string]$YaxiKeysFile)
. "$PSScriptRoot/scripts/environment.ps1"
if ($Native) {
    if (!(Get-Command cl.exe -ErrorAction SilentlyContinue)) { throw 'In einer x64 Developer PowerShell für Visual Studio mit C++-Buildtools starten.' }
    $package = Join-Path $buildRoot 'routex-client-cxx.0.2.0.zip'
    $sdk = Join-Path $buildRoot 'routex'
    if (!(Test-Path $package)) { Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/routex-client-cxx/0.2.0/routex-client-cxx.0.2.0.nupkg' -OutFile $package }
    if ((Get-FileHash $package).Hash -ne '512148E158CCEA0E39ADC6915C2BD90E6EC58E88006B3CED916154FFD2C0804E') { throw 'YAXI-Paket: SHA256 stimmt nicht überein.' }
    Expand-Archive $package $sdk -Force
    $lib = Join-Path $PSScriptRoot 'native/lib'
    New-Item -ItemType Directory -Force $lib | Out-Null
    Copy-Item "$sdk/runtimes/win-x64/native/routex_client_cxx.dll*" $lib -Force
    & cl.exe /nologo /std:c++20 /EHsc /MD /LD /utf-8 "/I$PSScriptRoot/native/include" "$PSScriptRoot/native/bridge.cpp" "/Fe$lib/sb_yaxi.dll" "/Fo$buildRoot/bridge.obj" /link "$lib/routex_client_cxx.dll.lib"
    if ($LASTEXITCODE -ne 0) { throw 'Native Build fehlgeschlagen.' }
    if (!$env:VCToolsRedistDir) { throw 'VCToolsRedistDir fehlt. C++-Buildtools-Umgebung laden.' }
    $crt = Get-ChildItem "$env:VCToolsRedistDir/x64" -Directory -Filter 'Microsoft.VC*.CRT' | Select-Object -First 1
    if (!$crt) { throw 'Visual-C++-Laufzeit nicht gefunden.' }
    Copy-Item "$($crt.FullName)/*.dll" $lib -Force
}
$properties = @()
if ($YaxiKeysFile) { $properties += "-p:YaxiKeysFile=$((Resolve-Path $YaxiKeysFile).Path)" }
& $dotnet restore "$PSScriptRoot/src/SimpleBanking.Windows" --configfile "$PSScriptRoot/NuGet.Config" @properties
if ($LASTEXITCODE -ne 0) { throw 'Restore fehlgeschlagen.' }
& $dotnet build "$PSScriptRoot/src/SimpleBanking.Windows" -c Release --no-restore --nologo @properties
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
if ($Test) {
    $testOptions = @()
    if ($YaxiKeysFile -or (Test-Path "$PSScriptRoot/secrets/yaxi-keys.json")) { $testOptions += '--require-embedded-keys' }
    & $dotnet "$PSScriptRoot/src/SimpleBanking.Windows/bin/Release/net10.0-windows/simplebanking.dll" --self-test --report "$buildRoot/test-results.json" @testOptions
    if ($LASTEXITCODE -ne 0) { throw 'Selbsttests fehlgeschlagen.' }
    & $dotnet "$PSScriptRoot/src/SimpleBanking.Windows/bin/Release/net10.0-windows/simplebanking.dll" --test-tray-lifecycle
    if ($LASTEXITCODE -ne 0) { throw 'Tray-Test fehlgeschlagen.' }
}
