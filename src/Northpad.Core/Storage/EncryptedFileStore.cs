using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Northpad.Core.Cryptography;
using Northpad.Core.Vault;

namespace Northpad.Core.Storage;

public sealed record StoredFile(Guid Id, string Kind, string Name, long Size, DateTimeOffset CreatedUtc);

public interface IFileStore
{
    StoredFile Import(string sourcePath, string kind);

    byte[] ReadBytes(Guid id);

    void Export(Guid id, string destinationPath);

    void Delete(Guid id);

    IReadOnlyList<StoredFile> List(string kind);
}

public sealed class EncryptedFileStore : IFileStore
{
    public const int MaxBytes = 25 * 1024 * 1024;

    private readonly SqliteDatabase _database;
    private readonly IContentProtector _protector;
    private readonly AppPaths _paths;

    public EncryptedFileStore(SqliteDatabase database, IContentProtector protector, AppPaths paths)
    {
        _database = database;
        _protector = protector;
        _paths = paths;
    }

    public StoredFile Import(string sourcePath, string kind)
    {
        RequireKind(kind);
        var info = new FileInfo(sourcePath);
        if (!info.Exists)
        {
            throw new StorageException("The file could not be found.");
        }

        if (info.Length > MaxBytes)
        {
            throw new StorageException("Files larger than 25 MB are not imported.");
        }

        var bytes = File.ReadAllBytes(info.FullName);
        var id = Guid.NewGuid();
        byte[] payload;
        try
        {
            payload = _protector.ProtectBytes(bytes, FieldContext.FileBody(id));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        _paths.EnsureDirectories();
        var destination = FilePath(id);
        var temporary = destination + ".tmp";
        File.WriteAllBytes(temporary, payload);
        File.Move(temporary, destination, overwrite: true);

        var stored = new StoredFile(id, kind, info.Name, info.Length, DateTimeOffset.UtcNow);
        try
        {
            _database.Execute(connection =>
            {
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO files (id, kind, name, size, created_utc)
                    VALUES ($id, $kind, $name, $size, $created);
                    """;
                command.Parameters.AddWithValue("$id", id.ToString("D"));
                command.Parameters.AddWithValue("$kind", kind);
                command.Parameters.Add("$name", SqliteType.Blob).Value = _protector.Protect(info.Name, FieldContext.FileName(id));
                command.Parameters.AddWithValue("$size", info.Length);
                command.Parameters.AddWithValue("$created", stored.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
                transaction.Commit();
            });
        }
        catch
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            throw;
        }

        return stored;
    }

    public byte[] ReadBytes(Guid id)
    {
        var path = FilePath(id);
        if (!File.Exists(path))
        {
            throw new StorageException("The file could not be found.");
        }

        var payload = File.ReadAllBytes(path);
        return _protector.UnprotectBytes(payload, FieldContext.FileBody(id));
    }

    public void Export(Guid id, string destinationPath)
    {
        var bytes = ReadBytes(id);
        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(destinationPath, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void Delete(Guid id)
    {
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM files WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            command.ExecuteNonQuery();
            transaction.Commit();
        });
        var path = FilePath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public IReadOnlyList<StoredFile> List(string kind)
    {
        RequireKind(kind);
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, kind, name, size, created_utc
                FROM files
                WHERE kind = $kind
                ORDER BY created_utc DESC;
                """;
            command.Parameters.AddWithValue("$kind", kind);
            using var reader = command.ExecuteReader();
            var files = new List<StoredFile>();
            while (reader.Read())
            {
                var id = Guid.Parse(reader.GetString(0));
                var name = _protector.Unprotect((byte[])reader.GetValue(2), FieldContext.FileName(id));
                var size = reader.GetInt64(3);
                var created = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                files.Add(new StoredFile(id, reader.GetString(1), name, size, created));
            }

            return (IReadOnlyList<StoredFile>)files;
        });
    }

    private string FilePath(Guid id) => Path.Combine(_paths.FilesDirectory, id.ToString("N") + ".npb");

    private static void RequireKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Length > 32)
        {
            throw new StorageException("The file type is not valid.");
        }

        foreach (var character in kind)
        {
            if (!char.IsAsciiLetter(character) && character != '-')
            {
                throw new StorageException("The file type is not valid.");
            }
        }
    }
}
