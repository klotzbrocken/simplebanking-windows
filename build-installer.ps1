param([string]$AppDirectory = "$PSScriptRoot\app", [string]$OutputFile = "$PSScriptRoot\simplebanking-setup.msi")
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/scripts/environment.ps1"
Initialize-Wix
if (!(Test-Path -LiteralPath $wix)) { throw 'WiX 4.0.6 fehlt. dotnet tool install wix --version 4.0.6 --tool-path work/tools/wix' }
$app = (Resolve-Path -LiteralPath $AppDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $app 'simplebanking.exe'))) { throw 'Zuerst publish.ps1 ausführen.' }
$intermediate = Join-Path $buildRoot 'installer'
New-Item -ItemType Directory -Force -Path $intermediate | Out-Null
$document = [xml]'<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" />'
function Add-Element($parent, [string]$name, [hashtable]$attributes) {
    $element = $document.CreateElement($name, 'http://wixtoolset.org/schemas/v4/wxs')
    foreach ($key in $attributes.Keys) { $element.SetAttribute($key, [string]$attributes[$key]) }
    [void]$parent.AppendChild($element)
    return $element
}
function Stable-Id([string]$path) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($path.ToLowerInvariant())
    return 'id' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).Substring(0,30)
}
$package = Add-Element $document.DocumentElement 'Package' @{Name='simplebanking'; Manufacturer='Maik Klotz'; Version=([xml](Get-Content "$PSScriptRoot/src/SimpleBanking.Windows/SimpleBanking.Windows.csproj" -Raw)).Project.PropertyGroup.Version; UpgradeCode='425BA152-E1C5-4E90-940E-FC056CA91AE9'; Scope='perUser'; Language='1031'; Compressed='yes'}
[void](Add-Element $package 'MajorUpgrade' @{DowngradeErrorMessage='Eine neuere Version von simplebanking ist bereits installiert.'})
[void](Add-Element $package 'MediaTemplate' @{EmbedCab='yes'})
[void](Add-Element $package 'Icon' @{Id='AppIcon'; SourceFile=(Join-Path $PSScriptRoot 'src\SimpleBanking.Windows\Assets\app.ico')})
[void](Add-Element $package 'Property' @{Id='ARPPRODUCTICON'; Value='AppIcon'})
$local = Add-Element $package 'StandardDirectory' @{Id='LocalAppDataFolder'}
$programs = Add-Element $local 'Directory' @{Id='UserPrograms'; Name='Programs'}
$install = Add-Element $programs 'Directory' @{Id='INSTALLFOLDER'; Name='simplebanking'}
$feature = Add-Element $package 'Feature' @{Id='Complete'; Title='simplebanking'; Level='1'}
$directories = @{''=$install}
foreach ($directory in Get-ChildItem -LiteralPath $app -Directory -Recurse | Sort-Object { $_.FullName.Length }) {
    $relative = [IO.Path]::GetRelativePath($app,$directory.FullName)
    $parent = [IO.Path]::GetDirectoryName($relative)
    $directories[$relative] = Add-Element $directories[$parent] 'Directory' @{Id=(Stable-Id "dir/$relative"); Name=$directory.Name}
}
foreach ($file in Get-ChildItem -LiteralPath $app -File -Recurse | Where-Object Extension -ne '.pdb') {
    $relative = [IO.Path]::GetRelativePath($app,$file.FullName)
    $id = Stable-Id "file/$relative"
    $component = Add-Element $directories[[IO.Path]::GetDirectoryName($relative)] 'Component' @{Id=$id; Guid='*'}
    [void](Add-Element $component 'File' @{Id=($id+'f'); Source=$file.FullName; KeyPath='yes'})
    [void](Add-Element $feature 'ComponentRef' @{Id=$id})
}
$menu = Add-Element $package 'StandardDirectory' @{Id='ProgramMenuFolder'}
$group = Add-Element $menu 'Directory' @{Id='AppMenu'; Name='simplebanking'}
$shortcuts = Add-Element $group 'Component' @{Id='MenuShortcuts'; Guid='08091B69-9350-42D5-B0C6-926D9118CA39'}
foreach ($mode in @(@{Id='MainShortcut';Name='simplebanking';Args=''},@{Id='DemoShortcut';Name='simplebanking Demo';Args='--demo'})) {
    $attributes=@{Id=$mode.Id;Name=$mode.Name;Target='[INSTALLFOLDER]simplebanking.exe';WorkingDirectory='INSTALLFOLDER';Icon='AppIcon'}
    if ($mode.Args) { $attributes.Arguments=$mode.Args }
    [void](Add-Element $shortcuts 'Shortcut' $attributes)
}
[void](Add-Element $shortcuts 'RemoveFolder' @{Id='RemoveAppMenu';On='uninstall'})
[void](Add-Element $shortcuts 'RegistryValue' @{Root='HKCU';Key='Software\simplebanking\Installer';Name='Menu';Type='integer';Value='1';KeyPath='yes'})
[void](Add-Element $feature 'ComponentRef' @{Id='MenuShortcuts'})
$desktop = Add-Element $package 'StandardDirectory' @{Id='DesktopFolder'}
$desktopComponent = Add-Element $desktop 'Component' @{Id='DesktopShortcut';Guid='70865673-EF41-49EF-94C0-03956676B1BB'}
[void](Add-Element $desktopComponent 'Shortcut' @{Id='DesktopMain';Name='simplebanking Windows';Target='[INSTALLFOLDER]simplebanking.exe';WorkingDirectory='INSTALLFOLDER';Icon='AppIcon'})
[void](Add-Element $desktopComponent 'RegistryValue' @{Root='HKCU';Key='Software\simplebanking\Installer';Name='Desktop';Type='integer';Value='1';KeyPath='yes'})
[void](Add-Element $feature 'ComponentRef' @{Id='DesktopShortcut'})
$source = Join-Path $intermediate 'simplebanking.wxs'
$document.Save($source)
& $wix build $source -arch x64 -o $OutputFile -intermediateFolder $intermediate
if ($LASTEXITCODE -ne 0) { throw 'MSI-Erstellung fehlgeschlagen.' }
Get-Item -LiteralPath $OutputFile | Select-Object FullName,Length
