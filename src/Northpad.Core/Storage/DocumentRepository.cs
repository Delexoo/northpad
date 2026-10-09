using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Northpad.Core.Cryptography;
using Northpad.Core.Vault;

namespace Northpad.Core.Storage;

public sealed record StoredDocument(
    Guid Id,
    string Kind,
    string Payload,
    string? SortKey,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public interface IDocumentRepository
{
    IReadOnlyList<StoredDocument> List(string kind);

    StoredDocument Create(string kind, string payload, string? sortKey);

    StoredDocument Update(Guid id, string payload, string? sortKey);

    void Delete(Guid id);
}

public sealed class DocumentRepository : IDocumentRepository
{
    public const int MaxPayloadBytes = 1_048_576;

    private readonly SqliteDatabase _database;
    private readonly IContentProtector _protector;

    public DocumentRepository(SqliteDatabase database, IContentProtector protector)
    {
        _database = database;
        _protector = protector;
    }

    public IReadOnlyList<StoredDocument> List(string kind)
    {
        RequireKind(kind);
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, kind, payload, sort_key, created_utc, updated_utc
                FROM documents
                WHERE kind = $kind
                ORDER BY COALESCE(sort_key, updated_utc) DESC;
                """;
            command.Parameters.AddWithValue("$kind", kind);
            using var reader = command.ExecuteReader();
            var items = new List<StoredDocument>();
            while (reader.Read())
            {
                items.Add(Read(reader));
            }

            return (IReadOnlyList<StoredDocument>)items;
        });
    }

    public StoredDocument Create(string kind, string payload, string? sortKey)
    {
        RequireKind(kind);
        RequirePayload(payload);
        var now = DateTimeOffset.UtcNow;
        var document = new StoredDocument(Guid.NewGuid(), kind, payload, Normalize(sortKey), now, now);
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO documents (id, kind, payload, sort_key, created_utc, updated_utc)
                VALUES ($id, $kind, $payload, $sort, $created, $updated);
                """;
            Bind(command, document);
            command.ExecuteNonQuery();
            transaction.Commit();
        });
        return document;
    }

    public StoredDocument Update(Guid id, string payload, string? sortKey)
    {
        RequirePayload(payload);
        var existing = Find(id) ?? throw new StorageException("The item could not be found.");
        var updated = existing with
        {
            Payload = payload,
            SortKey = Normalize(sortKey),
            UpdatedUtc = DateTimeOffset.UtcNow,
        };
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE documents
                SET payload = $payload, sort_key = $sort, updated_utc = $updated
                WHERE id = $id;
                """;
            Bind(command, updated);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new StorageException("The item could not be saved.");
            }

            transaction.Commit();
        });
        return updated;
    }

    public void Delete(Guid id)
    {
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM documents WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            command.ExecuteNonQuery();
            transaction.Commit();
        });
    }

    private StoredDocument? Find(Guid id)
    {
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, kind, payload, sort_key, created_utc, updated_utc
                FROM documents
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        });
    }

    private StoredDocument Read(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var kind = reader.GetString(1);
        var payload = _protector.Unprotect((byte[])reader.GetValue(2), FieldContext.Document(kind, id));
        var sort = reader.IsDBNull(3) ? null : reader.GetString(3);
        var created = ParseTime(reader.GetString(4));
        var updated = ParseTime(reader.GetString(5));
        return new StoredDocument(id, kind, payload, sort, created, updated);
    }

    private void Bind(SqliteCommand command, StoredDocument document)
    {
        command.Parameters.AddWithValue("$id", document.Id.ToString("D"));
        command.Parameters.AddWithValue("$kind", document.Kind);
        command.Parameters.Add("$payload", SqliteType.Blob).Value =
            _protector.Protect(document.Payload, FieldContext.Document(document.Kind, document.Id));
        command.Parameters.AddWithValue("$sort", (object?)document.SortKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", document.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", document.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void RequireKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Length > 32 || kind.Any(character => !char.IsAsciiLetter(character)))
        {
            throw new StorageException("The record type is not valid.");
        }
    }

    private static void RequirePayload(string payload)
    {
        if (Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
        {
            throw new StorageException("That item is too large to store.");
        }
    }

    private static string? Normalize(string? sortKey) =>
        string.IsNullOrWhiteSpace(sortKey) ? null : sortKey.Trim();

    private static DateTimeOffset ParseTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
