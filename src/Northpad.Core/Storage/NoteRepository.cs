using System.Globalization;
using Microsoft.Data.Sqlite;
using Northpad.Core.Cryptography;
using Northpad.Core.Models;
using Northpad.Core.Vault;

namespace Northpad.Core.Storage;

public interface INoteRepository
{
    IReadOnlyList<NoteRecord> List();

    NoteRecord? Find(Guid id);

    NoteRecord Create(string title, string body);

    NoteRecord Update(Guid id, string title, string body);

    void Delete(Guid id);
}

public sealed class NoteRepository : INoteRepository
{
    private readonly SqliteDatabase _database;
    private readonly IContentProtector _protector;

    public NoteRepository(SqliteDatabase database, IContentProtector protector)
    {
        _database = database;
        _protector = protector;
    }

    public IReadOnlyList<NoteRecord> List()
    {
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, title, body, created_utc, updated_utc
                FROM notes
                ORDER BY updated_utc DESC;
                """;
            using var reader = command.ExecuteReader();
            var notes = new List<NoteRecord>();
            while (reader.Read())
            {
                notes.Add(Read(reader));
            }

            return (IReadOnlyList<NoteRecord>)notes;
        });
    }

    public NoteRecord? Find(Guid id)
    {
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, title, body, created_utc, updated_utc
                FROM notes
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        });
    }

    public NoteRecord Create(string title, string body)
    {
        var now = DateTimeOffset.UtcNow;
        var note = new NoteRecord(Guid.NewGuid(), title, body, now, now);
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO notes (id, title, body, created_utc, updated_utc)
                VALUES ($id, $title, $body, $created, $updated);
                """;
            Bind(command, note);
            command.ExecuteNonQuery();
            transaction.Commit();
        });
        return note;
    }

    public NoteRecord Update(Guid id, string title, string body)
    {
        var existing = Find(id) ?? throw new StorageException("The note could not be found.");
        var updated = existing with { Title = title, Body = body, UpdatedUtc = DateTimeOffset.UtcNow };
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE notes
                SET title = $title, body = $body, updated_utc = $updated
                WHERE id = $id;
                """;
            Bind(command, updated);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new StorageException("The note could not be saved.");
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
            command.CommandText = "DELETE FROM notes WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            command.ExecuteNonQuery();
            transaction.Commit();
        });
    }

    private NoteRecord Read(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var title = _protector.Unprotect((byte[])reader.GetValue(1), FieldContext.NoteTitle(id));
        var body = _protector.Unprotect((byte[])reader.GetValue(2), FieldContext.NoteBody(id));
        var created = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var updated = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new NoteRecord(id, title, body, created, updated);
    }

    private void Bind(SqliteCommand command, NoteRecord note)
    {
        command.Parameters.AddWithValue("$id", note.Id.ToString("D"));
        command.Parameters.Add("$title", SqliteType.Blob).Value = _protector.Protect(note.Title, FieldContext.NoteTitle(note.Id));
        command.Parameters.Add("$body", SqliteType.Blob).Value = _protector.Protect(note.Body, FieldContext.NoteBody(note.Id));
        command.Parameters.AddWithValue("$created", note.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", note.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
    }
}
