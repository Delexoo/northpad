using Microsoft.Data.Sqlite;
using Northpad.Core.Vault;

namespace Northpad.Core.Storage;

public sealed class SqliteDatabase : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _connectionString;
    private bool _disposed;

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
        };
        _connectionString = builder.ToString();
    }

    public string DatabasePath { get; }

    public T Execute<T>(Func<SqliteConnection, T> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gate.Wait();
        try
        {
            using var connection = Open();
            return action(connection);
        }
        catch (SqliteException exception)
        {
            throw new StorageException("The local database could not complete the operation.", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Execute(Action<SqliteConnection> action)
    {
        Execute(connection =>
        {
            action(connection);
            return 0;
        });
    }

    public void Migrate()
    {
        Execute(connection =>
        {
            var version = ReadVersion(connection);
            if (version > SchemaMigrator.CurrentVersion)
            {
                throw new VaultStateException("This workspace was written by a newer version of Northpad. The database was not modified.");
            }

            if (version < SchemaMigrator.CurrentVersion)
            {
                using var transaction = connection.BeginTransaction();
                SchemaMigrator.Apply(connection, version);
                transaction.Commit();
            }
        });
    }

    public void Checkpoint()
    {
        Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            command.ExecuteNonQuery();
        });
    }

    public void ReleaseConnections()
    {
        _gate.Wait();
        try
        {
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ExecutePragma(connection, "PRAGMA foreign_keys = ON;");
        using (var journal = connection.CreateCommand())
        {
            journal.CommandText = "PRAGMA journal_mode = WAL;";
            journal.ExecuteScalar();
        }

        ExecutePragma(connection, "PRAGMA synchronous = FULL;");
        ExecutePragma(connection, "PRAGMA busy_timeout = 5000;");
        return connection;
    }

    private static void ExecutePragma(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int ReadVersion(SqliteConnection connection)
    {
        using var exists = connection.CreateCommand();
        exists.CommandText =
            """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table' AND name = 'schema_info';
            """;
        if (exists.ExecuteScalar() is null)
        {
            return 0;
        }

        using var version = connection.CreateCommand();
        version.CommandText = "SELECT version FROM schema_info WHERE id = 1;";
        var value = version.ExecuteScalar();
        return value is long number ? (int)number : 0;
    }
}

internal static class SchemaMigrator
{
    public const int CurrentVersion = 1;

    public static void Apply(SqliteConnection connection, int fromVersion)
    {
        if (fromVersion < 1)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE schema_info (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    version INTEGER NOT NULL
                );

                CREATE TABLE settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE notes (
                    id TEXT PRIMARY KEY,
                    title BLOB NOT NULL,
                    body BLOB NOT NULL,
                    created_utc TEXT NOT NULL,
                    updated_utc TEXT NOT NULL
                );

                CREATE TABLE tasks (
                    id TEXT PRIMARY KEY,
                    title BLOB NOT NULL,
                    details BLOB NOT NULL,
                    due_date TEXT,
                    priority INTEGER NOT NULL,
                    is_completed INTEGER NOT NULL,
                    created_utc TEXT NOT NULL,
                    updated_utc TEXT NOT NULL
                );

                INSERT INTO schema_info (id, version) VALUES (1, 1);
                """;
            command.ExecuteNonQuery();
        }
    }
}
