param([string]$DotnetPath = 'dotnet', [switch]$Publish)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & $DotnetPath restore NexusOptimizer.sln --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Restore falhou.' }
    & $DotnetPath build NexusOptimizer.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build falhou.' }
    & $DotnetPath test src/Nexus.Tests/Nexus.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam.' }
    if ($Publish) {
        & $DotnetPath publish src/Nexus.UI/Nexus.UI.csproj -c Release -r win-x64 --self-contained true -o artifacts/app
        if ($LASTEXITCODE -ne 0) { throw 'Publicação falhou.' }
    }
} finally { Pop-Location }
