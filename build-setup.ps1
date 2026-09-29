param(
    [string]$MsiFile = "$PSScriptRoot\simplebanking-setup.msi",
    [string]$AppDirectory = "$PSScriptRoot\app",
    [string]$OutputFile = "$PSScriptRoot\simplebanking-Beta-Setup.exe"
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/scripts/environment.ps1"
Initialize-Wix
$msi = (Resolve-Path -LiteralPath $MsiFile).Path
$exe = Join-Path (Resolve-Path -LiteralPath $AppDirectory).Path 'simplebanking.exe'
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion.Split('+')[0]
if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Ungültige Programmversion.' }
$extension = Join-Path $env:USERPROFILE '.wix\extensions\WixToolset.Bal.wixext\4.0.6\wixext4\WixToolset.Bal.wixext.dll'
if (!(Test-Path -LiteralPath $extension)) {
    & $wix extension add -g WixToolset.Bal.wixext/4.0.6
    if ($LASTEXITCODE -ne 0) { throw 'WiX-Bootstrapper-Erweiterung konnte nicht installiert werden.' }
}
$intermediate = Join-Path $buildRoot 'installer\bundle'
New-Item -ItemType Directory -Force -Path $intermediate | Out-Null
$msiXml = [Security.SecurityElement]::Escape($msi)
$iconXml = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'src\SimpleBanking.Windows\Assets\app.ico'))
$logoXml = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'src\SimpleBanking.Windows\Assets\app-mark.png'))
$localizationXml = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'installer\de-de.wxl'))
$licenseXml = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'installer\LICENSE.WIX.txt'))
$bundleVersion = ([Version]$version).ToString(3) + ".1"
$source = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:bal="http://wixtoolset.org/schemas/v4/wxs/bal">
  <Bundle Name="simplebanking Beta" Manufacturer="Maik Klotz" Version="$bundleVersion"
          UpgradeCode="782547C0-922D-4E39-91E6-589FB42B624E" IconSourceFile="$iconXml"
          AboutUrl="https://www.simplebanking.de/" DisableModify="no">
    <BootstrapperApplication>
      <Payload SourceFile="$licenseXml" Name="LICENSE.WIX.txt" />
      <Payload SourceFile="$localizationXml" Name="source\de-de.wxl" />
      <bal:WixStandardBootstrapperApplication Theme="hyperlinkLargeLicense" LicenseUrl=""
          LogoFile="$logoXml" LocalizationFile="$localizationXml" ShowVersion="yes" SuppressOptionsUI="yes"
          LaunchTarget="[LocalAppDataFolder]Programs\simplebanking\simplebanking.exe"
          LaunchWorkingFolder="[LocalAppDataFolder]Programs\simplebanking" />
    </BootstrapperApplication>
    <bal:Condition Message="simplebanking benötigt ein 64-Bit-Windows ab Windows 10." Condition="VersionNT64 &gt;= v10.0" />
    <Chain>
      <MsiPackage Id="SimplebankingApp" SourceFile="$msiXml" Compressed="yes" Vital="yes" Visible="no" />
    </Chain>
  </Bundle>
</Wix>
"@
$sourceFile = Join-Path $intermediate 'simplebanking-bundle.wxs'
[IO.File]::WriteAllText($sourceFile, $source)
& $wix build $sourceFile -arch x64 -ext $extension -culture de-de -o $OutputFile -intermediateFolder $intermediate
if ($LASTEXITCODE -ne 0) { throw 'Setup.exe-Erstellung fehlgeschlagen.' }
Get-Item -LiteralPath $OutputFile | Select-Object FullName,Length
