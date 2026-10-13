param([Parameter(Mandatory)][string]$ContentDirectory, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'This installer test may run only on a disposable GitHub-hosted runner, never on a customer PC.'
}
if (-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The official Windows unsigned-package test mechanism requires an administrator on the disposable runner.'
}
$testName = 'NexusOptimizer.StoreValidation.07'
if (Get-AppxPackage -Name $testName -AllUsers) { throw 'An existing test identity is present; refusing to modify it.' }
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$stage = Join-Path $runRoot 'test-content'
[IO.Directory]::CreateDirectory($stage) | Out-Null
Copy-Item -Path (Join-Path ([IO.Path]::GetFullPath($ContentDirectory)) '*') -Destination $stage -Recurse
[xml]$manifest = Get-Content -LiteralPath (Join-Path $stage 'AppxManifest.xml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($manifest.NameTable)
$ns.AddNamespace('m', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('v', 'http://schemas.microsoft.com/appx/manifest/virtualization/windows10')
$identity = $manifest.SelectSingleNode('/m:Package/m:Identity', $ns)
$identity.SetAttribute('Name', $testName)
$identity.SetAttribute('Publisher', 'CN=NEXUS Disposable Runner Test ONLY, OID.2.25.311729368913984317654407730594956997722=1')
$manifest.SelectSingleNode('/m:Package/m:Properties/v:FileSystemWriteVirtualization/v:ExcludedDirectories/v:ExcludedDirectory', $ns).InnerText = '$(KnownFolder:LocalAppData)\NexusOptimizer.StoreValidation.07'
$dataRoot = Join-Path $env:LOCALAPPDATA $testName
if (Test-Path -LiteralPath $dataRoot) { throw 'An existing data directory is present; refusing to reuse it.' }
$sdk = Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') -Directory | Where-Object Name -Match '^\d+\.\d+\.\d+\.\d+$' | Sort-Object { [version]$_.Name } -Descending | Where-Object { Test-Path (Join-Path $_.FullName 'x64/makeappx.exe') } | Select-Object -First 1
$makeappx = Join-Path $sdk.FullName 'x64/makeappx.exe'
$installed = $false
$checks = [ordered]@{ TestOnly = $true; IsDisposableHostedRunner = $true; Installed = $false; PackageIdentityVerified = $false; Updated = $false; DatabasePreservedAcrossUpdate = $false; Uninstalled = $false; DatabasePreservedAfterUninstall = $false; Reinstalled = $false; Pages = @(); WindowsSecurityChanged = $false; WindowsAdjustmentsApplied = $false; StoreCertification = 'NOT PERFORMED' }
function New-TestPackage([string]$version) {
    $identity.SetAttribute('Version', $version)
    $manifest.Save((Join-Path $stage 'AppxManifest.xml'))
    $path = Join-Path $runRoot "TEST-ONLY-DO-NOT-SUBMIT-$version.msix"
    & $makeappx pack /d $stage /p $path /no *> (Join-Path $runRoot "makeappx-test-$version.log")
    if ($LASTEXITCODE -ne 0) { throw 'Test package validation failed.' }
    return $path
}
function Invoke-PackageSmoke([string]$page, [string]$output) {
    $package = Get-AppxPackage -Name $testName
    if (-not $package) { throw 'Test package was not registered.' }
    $env:NEXUS_DATA_DIR = $output
    $env:NEXUS_SCREENSHOT_PAGE = $page
    $env:NEXUS_STORE_SCREENSHOT = '1'
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $process = Start-Process -FilePath (Join-Path $package.InstallLocation 'Nexus.UI.exe') -ArgumentList '--smoke-test' -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(45000)) { $process.Kill(); throw "Test-only package startup timed out: $page" }
    if ($process.ExitCode -ne 0) { throw "Package startup failed: $page ($($process.ExitCode))" }
    $result = Get-Content -LiteralPath (Join-Path $output 'smoke-test.json') -Raw | ConvertFrom-Json
    if ($result.PackageIdentity -ne $package.PackageFullName) { throw 'Process lacks the installed MSIX identity; portable startup is not a packaged test.' }
    if ($result.PrioritySelected -or $result.AnimationsSelected -or $result.PowerSelected) { throw 'Defaults selected a Windows change.' }
    if ($result.Width -lt 1366 -or $result.Height -lt 768) { throw 'Screenshot is too small for the Store.' }
    $checks.PackageIdentityVerified = $true
    $checks.Pages += [pscustomobject]@{ Page = $page; PackageIdentity = $result.PackageIdentity; Width = $result.Width; Height = $result.Height; DataDirectory = $result.DataDirectory; TemporaryDirectory = $result.TemporaryDirectory }
    $evidence = Join-Path $runRoot "screenshots/$page"
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    Copy-Item -LiteralPath (Join-Path $output 'dashboard.png'),(Join-Path $output 'smoke-test.json') -Destination $evidence
}
try {
    $first = New-TestPackage '0.7.0.0'
    Add-AppxPackage -Path $first -AllowUnsigned
    $installed = $true; $checks.Installed = $true
    foreach ($page in @('overview','optimize','gaming','tweaks','history')) { Invoke-PackageSmoke $page (Join-Path $dataRoot $page) }
    $database = Join-Path $dataRoot 'history/nexus.db'
    $databaseHash = (Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash
    $update = New-TestPackage '0.7.1.0'
    Add-AppxPackage -Path $update -AllowUnsigned
    if ((Get-AppxPackage -Name $testName).Version -ne [version]'0.7.1.0') { throw 'Package update did not occur.' }
    $checks.Updated = $true
    if ((Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash -ne $databaseHash) { throw 'Update changed or removed existing data.' }
    $checks.DatabasePreservedAcrossUpdate = $true
    Invoke-PackageSmoke 'overview' (Join-Path $dataRoot 'updated')
    Remove-AppxPackage -Package (Get-AppxPackage -Name $testName).PackageFullName -AllUsers
    $installed = $false; $checks.Uninstalled = $true
    if (Get-AppxPackage -Name $testName -AllUsers) { throw 'Test identity is still registered after uninstall.' }
    if ((Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash -ne $databaseHash) { throw 'Uninstall removed or changed the retained database.' }
    $checks.DatabasePreservedAfterUninstall = $true
    Add-AppxPackage -Path $first -AllowUnsigned
    $installed = $true; $checks.Reinstalled = $true
    Invoke-PackageSmoke 'history' (Join-Path $dataRoot 'history')
    $checks.Result = 'PASS'
}
catch { $checks.Result = 'FAIL'; $checks.Error = $_.Exception.ToString(); throw }
finally {
    if ($installed) {
        try { $ownPackage = Get-AppxPackage -Name $testName; if ($ownPackage) { Remove-AppxPackage -Package $ownPackage.PackageFullName -AllUsers } }
        catch { $checks.CleanupError = $_.Exception.Message }
    }
    $checks | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'packaged-validation.json') -Encoding utf8
    Remove-Item Env:/NEXUS_DATA_DIR,Env:/NEXUS_SCREENSHOT_PAGE,Env:/NEXUS_STORE_SCREENSHOT -ErrorAction SilentlyContinue
}
