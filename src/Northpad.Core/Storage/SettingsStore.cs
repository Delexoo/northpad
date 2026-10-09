using Microsoft.Data.Sqlite;

namespace Northpad.Core.Storage;

public interface ISettingsStore
{
    string? Get(string key);

    void Set(string key, string value);
}

public sealed class SettingsStore : ISettingsStore
{
    private readonly SqliteDatabase _database;

    public SettingsStore(SqliteDatabase database)
    {
        _database = database;
    }

    public string? Get(string key)
    {
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = $key;";
            command.Parameters.AddWithValue("$key", key);
            var value = command.ExecuteScalar();
            return value as string;
        });
    }

    public void Set(string key, string value)
    {
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO settings (key, value)
                VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
            transaction.Commit();
        });
    }
}
