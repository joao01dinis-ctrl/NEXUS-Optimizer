param([Parameter(Mandatory)][string]$ContentDirectory, [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$InitialVersion = '0.10.0.0', [string]$UpdateVersion = '0.10.1.0')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'This installer test may run only on a disposable GitHub-hosted runner, never on a customer PC.'
}
if (-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The official Windows unsigned-package test mechanism requires an administrator on the disposable runner.'
}
# The legacy test identity stays fixed to match the guarded in-app Undo test; it is never the production identity.
if ($InitialVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or $UpdateVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or [version]$UpdateVersion -le [version]$InitialVersion) { throw 'Invalid installer test versions.' }
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
$checks = [ordered]@{ TestOnly = $true; IsDisposableHostedRunner = $true; Installed = $false; PackageIdentityVerified = $false; PackagedWindowsPreferenceAndUndoVerified = $false; Updated = $false; DatabasePreservedAcrossUpdate = $false; Uninstalled = $false; DatabasePreservedAfterUninstall = $false; Reinstalled = $false; Pages = @(); WindowsSecurityChanged = $false; WindowsAdjustmentsApplied = $false; AllTestAdjustmentsRestored = $false; StoreCertification = 'NOT PERFORMED' }
# The hosted Hyper-V desktop defaults to 1024x768. Select an advertised larger
# mode temporarily so the real window can render Store-size images; never scale a PNG.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
public static class NexusHostedDesktop {
    [StructLayout(LayoutKind.Explicit, Size=220)]
    public struct Mode {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(70)] public ushort Extra;
        [FieldOffset(72)] public uint Fields;
        [FieldOffset(168)] public uint Bits;
        [FieldOffset(172)] public uint Width;
        [FieldOffset(176)] public uint Height;
    }
    [DllImport("user32.dll", EntryPoint="EnumDisplaySettingsW", CharSet=CharSet.Unicode)]
    static extern bool Enumerate(string device, int index, ref Mode mode);
    [DllImport("user32.dll", EntryPoint="ChangeDisplaySettingsExW", CharSet=CharSet.Unicode)]
    static extern int Change(string device, ref Mode mode, IntPtr window, uint flags, IntPtr parameter);
    static Mode original;
    static bool changed;
    static Mode Current() { var m=new Mode { Size=220 }; if (!Enumerate(null,-1,ref m)) throw new InvalidOperationException("Cannot read hosted display mode."); return m; }
    public static string Prepare() {
        original=Current();
        if (original.Width>=1600 && original.Height>=1000) return original.Width+"x"+original.Height;
        var candidates=new List<Mode>();
        for (int i=0;i<1000;i++) { var m=new Mode { Size=220 }; if (!Enumerate(null,i,ref m)) break; if (m.Width>=1600 && m.Height>=1000 && m.Bits==32) candidates.Add(m); }
        foreach (var candidate in candidates.OrderBy(m=>(long)m.Width*m.Height)) {
            var mode=candidate; mode.Fields=0x00080000|0x00100000;
            if (Change(null,ref mode,IntPtr.Zero,2,IntPtr.Zero)!=0) continue;
            if (Change(null,ref mode,IntPtr.Zero,0,IntPtr.Zero)!=0) continue;
            changed=true; var actual=Current();
            if (actual.Width>=1600 && actual.Height>=1000) return actual.Width+"x"+actual.Height;
        }
        throw new InvalidOperationException("The hosted display has no usable Store-size mode; real screenshot validation cannot proceed.");
    }
    public static void Restore() { if (changed && Change(null,ref original,IntPtr.Zero,0,IntPtr.Zero)!=0) throw new InvalidOperationException("Could not restore the hosted desktop mode."); changed=false; }
}
'@
function New-TestPackage([string]$version) {
    $identity.SetAttribute('Version', $version)
    $manifest.Save((Join-Path $stage 'AppxManifest.xml'))
    & (Join-Path $PSScriptRoot 'New-NexusPackageResources.ps1') -ContentDirectory $stage -IdentityName $testName -SdkBin (Split-Path $makeappx -Parent)
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
    if ($process.ExitCode -ne 0) {
        $failure = Join-Path $runRoot 'startup-diagnostics'
        [IO.Directory]::CreateDirectory($failure) | Out-Null
        Copy-Item -Path (Join-Path $output '*') -Destination $failure -Recurse -ErrorAction SilentlyContinue
        Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = [DateTime]::Now.AddMinutes(-3) } -ErrorAction SilentlyContinue |
            Where-Object Message -Match 'Nexus.UI|Microsoft.UI.Xaml|NEXUS' |
            Select-Object TimeCreated,Id,ProviderName,Message | ConvertTo-Json -Depth 4 |
            Set-Content -LiteralPath (Join-Path $failure 'application-events.json') -Encoding utf8
        $startupError = Join-Path $output 'startup-error.txt'
        if (Test-Path -LiteralPath $startupError) { Write-Output (Get-Content -LiteralPath $startupError -Raw) }
        throw "Package startup failed: $page ($($process.ExitCode))"
    }
    $result = Get-Content -LiteralPath (Join-Path $output 'smoke-test.json') -Raw | ConvertFrom-Json
    $evidence = Join-Path $runRoot "screenshots/$page"
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    Copy-Item -LiteralPath (Join-Path $output 'dashboard.png'),(Join-Path $output 'smoke-test.json') -Destination $evidence
    if (Test-Path -LiteralPath (Join-Path $output 'packaged-undo.json')) { Copy-Item -LiteralPath (Join-Path $output 'packaged-undo.json') -Destination $evidence }
    foreach ($name in @('library-vpn-readonly.json','protection-readonly.json')) {
        $record = Join-Path $output $name
        if (Test-Path -LiteralPath $record) { Copy-Item -LiteralPath $record -Destination $evidence }
    }
    if ($page -eq 'vpn') {
        $vpn = Get-Content -LiteralPath (Join-Path $output 'library-vpn-readonly.json') -Raw | ConvertFrom-Json
        if ($vpn.Result -ne 'PASS' -or $vpn.VpnControlRequests -ne 0 -or $vpn.VpnExternalSamples -ne 0) { throw 'Packaged VPN issued an unauthorized operation.' }
    }
    if ($page -eq 'protection') {
        $protection = Get-Content -LiteralPath (Join-Path $output 'protection-readonly.json') -Raw | ConvertFrom-Json
        if ($protection.Result -ne 'PASS' -or $protection.OperationsRequested -ne 0) { throw 'Packaged protection issued an unauthorized request.' }
    }
    if ($result.PackageIdentity -ne $package.PackageFullName) { throw 'Process lacks the installed MSIX identity; portable startup is not a packaged test.' }
    if ($result.PrioritySelected -or $result.AnimationsSelected -or $result.PowerSelected) { throw 'Defaults selected a Windows change.' }
    Write-Host "Rendered packaged $page at $($result.Width)x$($result.Height) pixels."
    if ($result.Width -lt 1366 -or $result.Height -lt 768) { throw "Screenshot is too small for the Store: $($result.Width)x$($result.Height)." }
    $checks.PackageIdentityVerified = $true
    $checks.Pages += [pscustomobject]@{ Page = $page; PackageIdentity = $result.PackageIdentity; Width = $result.Width; Height = $result.Height; DataDirectory = $result.DataDirectory; TemporaryDirectory = $result.TemporaryDirectory }
    if ($env:NEXUS_TEST_PACKAGED_UNDO -eq '1') {
        $undo = Get-Content -LiteralPath (Join-Path $output 'packaged-undo.json') -Raw | ConvertFrom-Json
        if ($undo.Result -ne 'PASS' -or $undo.Original -ne $undo.Restored -or $undo.Original -eq $undo.Applied -or $undo.JournalState -ne 'reverted' -or $undo.PackageIdentity -ne $package.PackageFullName) {
            throw 'Actual packaged Windows preference and Undo validation failed.'
        }
        Copy-Item -LiteralPath (Join-Path $output 'packaged-undo.json') -Destination $evidence
        $checks.PackagedWindowsPreferenceAndUndoVerified = $true
        $checks.WindowsAdjustmentsApplied = $true
        $checks.AllTestAdjustmentsRestored = $true
    }
}
try {
    $checks.HostedDesktopResolution = [NexusHostedDesktop]::Prepare()
    Write-Host "Hosted desktop: $($checks.HostedDesktopResolution)"
    $first = New-TestPackage $InitialVersion
    Add-AppxPackage -Path $first -AllowUnsigned
    $installed = $true; $checks.Installed = $true
    $env:NEXUS_TEST_PACKAGED_UNDO = '1'
    Invoke-PackageSmoke 'overview' (Join-Path $dataRoot 'overview')
    Remove-Item Env:/NEXUS_TEST_PACKAGED_UNDO
    foreach ($page in @('optimize','gaming','tweaks','vpn','protection','history')) { Invoke-PackageSmoke $page (Join-Path $dataRoot $page) }
    $database = Join-Path $dataRoot 'history/nexus.db'
    $databaseHash = (Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash
    $update = New-TestPackage $UpdateVersion
    Add-AppxPackage -Path $update -AllowUnsigned
    if ((Get-AppxPackage -Name $testName).Version -ne [version]$UpdateVersion) { throw 'Package update did not occur.' }
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
    try { [NexusHostedDesktop]::Restore(); $checks.HostedDesktopRestored = $true }
    catch { $checks.DesktopCleanupError = $_.Exception.Message }
    if ($installed) {
        try { $ownPackage = Get-AppxPackage -Name $testName; if ($ownPackage) { Remove-AppxPackage -Package $ownPackage.PackageFullName -AllUsers } }
        catch { $checks.CleanupError = $_.Exception.Message }
    }
    $checks | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'packaged-validation.json') -Encoding utf8
    Remove-Item Env:/NEXUS_DATA_DIR,Env:/NEXUS_SCREENSHOT_PAGE,Env:/NEXUS_STORE_SCREENSHOT,Env:/NEXUS_TEST_PACKAGED_UNDO -ErrorAction SilentlyContinue
}
