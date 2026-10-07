# Third-party components

The MIT licence in LICENSE applies to NEXUS-owned source code. Third-party
components remain subject to their original terms. This file identifies the
direct dependencies; it does not replace their full licence texts.

| Component | Package / source |
|---|---|
| Windows App SDK and WinUI | https://www.nuget.org/packages/Microsoft.WindowsAppSDK/1.8.260710003 |
| CommunityToolkit.Mvvm | https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.0 |
| Microsoft.Data.Sqlite | https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12 |
| SQLite native library security override | https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.50.3 |
| System.Management | https://www.nuget.org/packages/System.Management/10.0.0 |
| Serilog | https://www.nuget.org/packages/Serilog/4.3.0 |
| Serilog.Sinks.File | https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0 |
| .NET runtime | https://github.com/dotnet/runtime |

SQLitePCLRaw, SQLite, Windows SDK projections and other transitive dependencies
are resolved by NuGet. The test toolchain also uses Microsoft.NET.Test.Sdk,
xUnit and xunit.runner.visualstudio. Inspect the package licence metadata and
retained runtime notices before redistributing or changing dependency versions.

NEXUS does not claim ownership of or permission to re-sign upstream binaries.
