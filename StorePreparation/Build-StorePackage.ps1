<#
.SYNOPSIS
Builds an unsigned MSIX for later Store review, or a conspicuously marked offline schema test.
.DESCRIPTION
Does not install, sign, submit, change Windows settings, or run NEXUS. Production identity must be
copied exactly from Partner Center. MakeAppx validation is not Store certification or a launch test.
#>
[CmdletBinding(DefaultParameterSetName = 'PartnerCenter')]
param(
    [Parameter(Mandatory, ParameterSetName = 'PartnerCenter')]
    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')][string]$IdentityName,
    [Parameter(Mandatory, ParameterSetName = 'PartnerCenter')]
    [ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory, ParameterSetName = 'PartnerCenter')]
    [ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [Parameter(Mandatory, ParameterSetName = 'PartnerCenter')]
    [switch]$PartnerCenterIdentityConfirmed,
    [Parameter(Mandatory, ParameterSetName = 'SchemaTest')]
    [switch]$SchemaValidationTest,
    [string]$AppDisplayName = 'NEXUS Optimizer',
    [ValidatePattern('^\d{1,5}\.\d{1,5}\.\d{1,5}\.0$')][string]$PackageVersion = '0.7.0.0',
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '../NexusOptimizer/artifacts/app-v0.7-store'),
    [string]$SdkBin
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$preparationRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path
if ($sourceRoot.StartsWith('\\')) { throw 'Use a local publication folder, not a network share.' }
foreach ($part in $PackageVersion.Split('.')) { if ([int]$part -gt 65535) { throw 'Package version component is above 65535.' } }
if ($PackageVersion -eq '0.0.0.0') { throw 'Package version cannot be zero.' }
if ($SchemaValidationTest) {
    $IdentityName = 'NexusOptimizer.SchemaValidationOnly'
    $Publisher = 'CN=NEXUS Offline Schema Test DO NOT SUBMIT'
    $PublisherDisplayName = 'OFFLINE TEST ONLY - NOT A STORE PUBLISHER'
    $AppDisplayName = 'NEXUS SCHEMA TEST - DO NOT SUBMIT'
} else {
    if (-not $PartnerCenterIdentityConfirmed) { throw 'Confirm that all identity values were copied from Partner Center.' }
    foreach ($value in @($IdentityName, $Publisher, $PublisherDisplayName, $AppDisplayName)) {
        if ($value -match '(?i)(@@|placeholder|replace.?me|change.?me|TODO|schema.?test|offline.?test|DO NOT SUBMIT)' -or $value -match '[<>]') {
            throw 'Identity contains a template/test marker. Copy the exact real values from Partner Center.'
        }
    }
    # Parses the distinguished name only. It creates no certificate and changes no trust store.
    [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new($Publisher) | Out-Null
}
if ($AppDisplayName.Length -gt 256 -or $PublisherDisplayName.Length -gt 256) { throw 'Display names are too long.' }
foreach ($required in @('Nexus.UI.exe', 'Nexus.UI.dll', 'Nexus.UI.runtimeconfig.json', 'Nexus.UI.deps.json',
    'Nexus.UI.pri', 'App.xbf', 'MainWindow.xbf', 'coreclr.dll', 'System.Private.CoreLib.dll', 'Microsoft.ui.xaml.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $required) -PathType Leaf)) { throw "Missing publication component: $required" }
}
if (-not $SdkBin) {
    $sdkBase = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $sdkCandidates = Get-ChildItem -LiteralPath $sdkBase -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending
    foreach ($candidate in $sdkCandidates) {
        $candidateBin = Join-Path $candidate.FullName 'x64'
        if (Test-Path -LiteralPath (Join-Path $candidateBin 'makeappx.exe')) { $SdkBin = $candidateBin; break }
    }
}
if (-not $SdkBin) { throw 'Windows SDK MakeAppx was not found. Supply -SdkBin.' }
$makeAppx = Join-Path ([System.IO.Path]::GetFullPath($SdkBin)) 'makeappx.exe'
if (-not (Test-Path -LiteralPath $makeAppx -PathType Leaf)) { throw 'MakeAppx was not found in SdkBin.' }

# A fresh workspace directory on every run avoids overwriting earlier packages or source files.
$runName = ('{0}-{1}' -f $(if ($SchemaValidationTest) { 'TEST-ONLY-DO-NOT-SUBMIT' } else { 'Store-Draft' }), [guid]::NewGuid().ToString('N'))
$runRoot = [System.IO.Path]::GetFullPath((Join-Path $preparationRoot "builds/$runName"))
if (-not $runRoot.StartsWith($preparationRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Packaging destination must remain inside StorePreparation.'
}
$stage = Join-Path $runRoot 'content'
[System.IO.Directory]::CreateDirectory($stage) | Out-Null
$inputManifest = [System.Collections.Generic.List[object]]::new()
foreach ($entry in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force) {
    if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Publication has a reparse point: $($entry.FullName)" }
    if ($entry.PSIsContainer) { continue }
    if ($entry.Extension -match '^\.(pfx|p12|pem|key)$') { throw 'A publication must not contain private key material.' }
    if ($entry.Name -in @('AppxManifest.xml', 'AppxSignature.p7x', 'AppxBlockMap.xml', '[Content_Types].xml')) {
        throw 'Use the unpackaged self-contained publication, not a previously packaged folder.'
    }
    $relative = $entry.FullName.Substring($sourceRoot.TrimEnd('\', '/').Length).TrimStart('\', '/')
    $destination = Join-Path $stage $relative
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $entry.FullName -Destination $destination
    $sourceHash = (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash
    $copyHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $copyHash) { throw "Publication changed during packaging: $relative" }
    $inputManifest.Add([pscustomobject]@{ Path = $relative; Bytes = $entry.Length; SHA256 = $copyHash })
}
& (Join-Path $preparationRoot 'New-NexusAssets.ps1') -Destination (Join-Path $stage 'Assets')
[xml]$manifest = Get-Content -LiteralPath (Join-Path $preparationRoot 'AppxManifest.xml.template') -Raw
$namespace = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
$namespace.AddNamespace('m', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$namespace.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
$identity = $manifest.SelectSingleNode('/m:Package/m:Identity', $namespace)
$identity.SetAttribute('Name', $IdentityName); $identity.SetAttribute('Publisher', $Publisher); $identity.SetAttribute('Version', $PackageVersion)
$manifest.SelectSingleNode('/m:Package/m:Properties/m:DisplayName', $namespace).InnerText = $AppDisplayName
$manifest.SelectSingleNode('/m:Package/m:Properties/m:PublisherDisplayName', $namespace).InnerText = $PublisherDisplayName
$manifest.SelectSingleNode('/m:Package/m:Applications/m:Application/uap:VisualElements', $namespace).SetAttribute('DisplayName', $AppDisplayName)
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
$inputManifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runRoot 'input-files.json') -Encoding utf8
$packageName = if ($SchemaValidationTest) { 'TEST-ONLY-DO-NOT-INSTALL-OR-SUBMIT.msix' } else { "NEXUS-$PackageVersion-x64-UNSIGNED-STORE-DRAFT.msix" }
$packagePath = Join-Path $runRoot $packageName
$validationLog = Join-Path $runRoot 'makeappx-validation.log'
# /nv is deliberately absent: MakeAppx performs its normal schema/semantic validation.
& $makeAppx pack /d $stage /p $packagePath /no 2>&1 | Out-File -LiteralPath $validationLog -Encoding utf8
$makeAppxExitCode = $LASTEXITCODE
if ($makeAppxExitCode -ne 0) { throw "MakeAppx validation failed ($makeAppxExitCode). See $validationLog" }
$report = [pscustomobject]@{
    SchemaValidationTest = [bool]$SchemaValidationTest
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
    SourceDirectory = $sourceRoot
    SourceFileCount = $inputManifest.Count
    SourceBytes = ($inputManifest | Measure-Object -Property Bytes -Sum).Sum
    MakeAppx = $makeAppx
    MakeAppxExitCode = $makeAppxExitCode
    Package = $packagePath
    PackageBytes = (Get-Item -LiteralPath $packagePath).Length
    PackageSHA256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
    Signed = $false
    Installed = $false
    AppExecuted = $false
    StoreSubmitted = $false
    Certification = 'NOT PERFORMED'
    FunctionalPackagedValidation = 'NOT PERFORMED'
}
$report | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runRoot 'packaging-report.json') -Encoding utf8
$report | Format-List
Write-Warning 'This package is unsigned and has not been installed, launched or certified. Do not announce Store readiness.'
if ($SchemaValidationTest) { Write-Warning 'TEST IDENTITY ONLY. Never install or submit this package.' }
