param([Parameter(Mandatory)][string]$ContentDirectory, [Parameter(Mandatory)][string]$IdentityName, [Parameter(Mandatory)][string]$SdkBin)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath($ContentDirectory)
$appPri = Join-Path $root 'Nexus.UI.pri'
if (-not (Test-Path -LiteralPath $appPri -PathType Leaf)) { throw 'Missing merged application/WinUI resource index.' }
$makepri = Join-Path $SdkBin 'makepri.exe'
if (-not (Test-Path -LiteralPath $makepri -PathType Leaf)) { throw 'Windows SDK MakePri was not found.' }
# The portable index contains the app and embedded WinUI themes. MSIX needs the default
# resources.pri with the actual package resource-map identity. Do not replace any binaries.
$config = Join-Path (Split-Path $root -Parent) 'package-priconfig.xml'
$configXml = [xml]'<resources targetOsVersion="10.0.0" majorVersion="1"><index root="" startIndexAt="Nexus.UI.pri"><default><qualifier name="Language" value="pt-PT" /></default><indexer-config type="PRI" /></index></resources>'
$configXml.resources.index.SetAttribute('root', $root)
$configXml.Save($config)
$log = Join-Path (Split-Path $root -Parent) 'makepri-package.log'
& $makepri new /pr $root /cf $config /in $IdentityName /of (Join-Path $root 'resources.pri') /o *> $log
if ($LASTEXITCODE -ne 0) { throw "Packaged resource-index generation failed. See $log" }
$dump = Join-Path (Split-Path $root -Parent) 'package-resources.xml'
& $makepri dump /if (Join-Path $root 'resources.pri') /of $dump /o *>> $log
if ($LASTEXITCODE -ne 0) { throw 'Packaged resource-index verification failed.' }
[xml]$resources = Get-Content -LiteralPath $dump -Raw
$map = $resources.PriInfo.ResourceMap
if ($map.name -ne $IdentityName) { throw 'Resource map does not match the package identity.' }
if (-not $resources.SelectSingleNode("//NamedResource[@name='App.xbf']") -or
    -not $resources.SelectSingleNode("//NamedResource[@name='MainWindow.xbf']") -or
    -not $resources.SelectSingleNode("//ResourceMapSubtree[@name='Themes']")) {
    throw 'The packaged resource index lacks application or WinUI theme resources.'
}
Write-Output "Packaged WinUI resources verified for $IdentityName."
