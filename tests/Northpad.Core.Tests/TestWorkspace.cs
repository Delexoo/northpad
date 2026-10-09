using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.Core.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace(int iterations = 1_000)
    {
        Root = Path.Combine(Path.GetTempPath(), "northpad-tests", Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Root);
        Database = new SqliteDatabase(Paths.DatabasePath);
        Vault = new VaultService(Paths, Database, NullLogger<VaultService>.Instance, iterations);
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public SqliteDatabase Database { get; }

    public VaultService Vault { get; }

    public void Dispose()
    {
        Vault.Dispose();
        Database.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
