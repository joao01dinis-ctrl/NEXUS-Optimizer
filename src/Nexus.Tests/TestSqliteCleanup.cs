using Microsoft.Data.Sqlite;

namespace Nexus.Tests;

internal static class TestSqliteCleanup
{
    // Clearing every process-wide pool races unrelated parallel tests. Release only this fixture's pools.
    public static void ReleasePools(string root)
    {
        if(!Directory.Exists(root))return;
        foreach(var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
            .Where(f=>Path.GetExtension(f) is ".db" or ".sqlite"))
        {
            using var defaults=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=file}.ToString());
            using var explicitMode=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=file,Mode=SqliteOpenMode.ReadWriteCreate}.ToString());
            SqliteConnection.ClearPool(defaults);SqliteConnection.ClearPool(explicitMode);
        }
    }
}
