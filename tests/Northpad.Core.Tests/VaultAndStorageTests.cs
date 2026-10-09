using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Northpad.Core.Cryptography;
using Northpad.Core.Modules;
using Northpad.Core.Search;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.Core.Tests;

public class VaultAndStorageTests
{
    [Fact]
    public void New_vault_roundtrips_notes_without_writing_plaintext()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var notes = new NoteRepository(workspace.Database, workspace.Vault.Protector);
        const string secret = "SuperSecretPhrase-XYZ-笔记";
        var note = notes.Create("Private title " + secret, "Body " + secret);
        var loaded = notes.Find(note.Id);
        Assert.NotNull(loaded);
        Assert.Equal(note.Title, loaded.Title);
        Assert.Equal(note.Body, loaded.Body);

        workspace.Database.Checkpoint();
        workspace.Database.ReleaseConnections();
        var haystack = ReadAllFiles(workspace.Root);
        Assert.DoesNotContain(secret, haystack);
    }

    [Fact]
    public void Tampered_note_ciphertext_is_rejected()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var notes = new NoteRepository(workspace.Database, workspace.Vault.Protector);
        var note = notes.Create("Title", "Body text");
        workspace.Database.Execute(connection =>
        {
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT body FROM notes WHERE id = $id;";
            read.Parameters.AddWithValue("$id", note.Id.ToString("D"));
            var body = (byte[])read.ExecuteScalar()!;
            body[^1] ^= 0x5A;
            using var write = connection.CreateCommand();
            write.CommandText = "UPDATE notes SET body = $body WHERE id = $id;";
            write.Parameters.Add("$body", SqliteType.Blob).Value = body;
            write.Parameters.AddWithValue("$id", note.Id.ToString("D"));
            write.ExecuteNonQuery();
        });

        Assert.ThrowsAny<CryptographicException>(() => notes.Find(note.Id));
    }

    [Fact]
    public void Tasks_roundtrip_completion_priority_and_due_date()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var tasks = new TaskRepository(workspace.Database, workspace.Vault.Protector);
        var due = new DateOnly(2026, 10, 9);
        var created = tasks.Create("Ship notes", "Check encryption", due, Models.TaskPriority.High);
        var updated = tasks.Update(created with { IsCompleted = true, Details = "Checked" });
        var loaded = tasks.Find(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Ship notes", loaded.Title);
        Assert.Equal("Checked", loaded.Details);
        Assert.Equal(due, loaded.DueDate);
        Assert.Equal(Models.TaskPriority.High, loaded.Priority);
        Assert.True(loaded.IsCompleted);
        Assert.Equal(updated.UpdatedUtc, loaded.UpdatedUtc);
    }

    [Fact]
    public void Failed_transaction_does_not_keep_the_row()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        Assert.Throws<InvalidOperationException>(() =>
            workspace.Database.Execute(connection =>
            {
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO settings (key, value) VALUES ('temp', 'value');";
                command.ExecuteNonQuery();
                throw new InvalidOperationException("fail");
            }));

        var settings = new SettingsStore(workspace.Database);
        Assert.Null(settings.Get("temp"));
    }

    [Fact]
    public void Migration_is_idempotent_and_refuses_newer_schemas()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Database.Migrate();
        var version = workspace.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT version FROM schema_info WHERE id = 1;";
            return (long)command.ExecuteScalar()!;
        });
        Assert.Equal(1, version);

        workspace.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_info SET version = 99 WHERE id = 1;";
            command.ExecuteNonQuery();
        });
        var error = Assert.Throws<VaultStateException>(() => workspace.Database.Migrate());
        Assert.Contains("newer version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_key_does_not_get_replaced_when_a_database_exists()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        File.Delete(workspace.Paths.KeyPath);
        var error = Assert.Throws<VaultStateException>(() => workspace.Vault.EnsureCreated());
        Assert.Contains("did not create a replacement key", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(workspace.Paths.KeyPath));
    }

    [Fact]
    public void Wrong_passphrase_does_not_change_the_key_file_or_reveal_data()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var notes = new NoteRepository(workspace.Database, workspace.Vault.Protector);
        notes.Create("Title", "hidden body");
        var recovery = workspace.Vault.EnablePassphrase("correct-passphrase");
        workspace.Vault.Lock();
        var before = File.ReadAllBytes(workspace.Paths.KeyPath);

        var error = Assert.Throws<VaultUnlockFailedException>(() => workspace.Vault.UnlockWithPassphrase("wrong-passphrase"));
        Assert.Equal("That passphrase was not accepted.", error.Message);
        Assert.False(workspace.Vault.IsUnlocked);
        Assert.Equal(before, File.ReadAllBytes(workspace.Paths.KeyPath));

        workspace.Vault.UnlockWithPassphrase("correct-passphrase");
        Assert.Equal("hidden body", notes.Find(notes.List()[0].Id)!.Body);
        workspace.Vault.Lock();
        workspace.Vault.UnlockWithRecoveryKey(recovery);
        Assert.True(workspace.Vault.IsUnlocked);
    }

    [Fact]
    public void Changing_passphrase_keeps_the_same_data_and_rejects_the_old_one()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var notes = new NoteRepository(workspace.Database, workspace.Vault.Protector);
        var note = notes.Create("Kept", "still here");
        workspace.Vault.EnablePassphrase("first-passphrase");
        workspace.Vault.ChangePassphrase("first-passphrase", "second-passphrase");
        workspace.Vault.Lock();
        Assert.Throws<VaultUnlockFailedException>(() => workspace.Vault.UnlockWithPassphrase("first-passphrase"));
        workspace.Vault.UnlockWithPassphrase("second-passphrase");
        Assert.Equal("still here", notes.Find(note.Id)!.Body);
    }

    [Fact]
    public void Passphrase_is_not_stored_in_the_key_file()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        const string passphrase = "unique-passphrase-value";
        workspace.Vault.EnablePassphrase(passphrase);
        var file = Encoding.UTF8.GetString(File.ReadAllBytes(workspace.Paths.KeyPath));
        Assert.DoesNotContain(passphrase, file);
    }

    [Fact]
    public void Delete_all_data_removes_previous_plaintext_ability()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var notes = new NoteRepository(workspace.Database, workspace.Vault.Protector);
        notes.Create("Gone", "delete-me-please");
        workspace.Vault.DeleteAllData();
        Assert.Empty(notes.List());
        workspace.Database.Checkpoint();
        workspace.Database.ReleaseConnections();
        Assert.DoesNotContain("delete-me-please", ReadAllFiles(workspace.Root));
    }

    [Fact]
    public void Search_matches_note_bodies_and_ignores_blank_queries()
    {
        Assert.Empty(LocalSearch.Find("   ", [], []));
        var note = new Models.NoteRecord(Guid.NewGuid(), "Shopping", "Buy oats", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var hits = LocalSearch.Find("oats", [note], []);
        Assert.Single(hits);
        Assert.Equal(KnownModules.Notes, hits[0].ModuleId);
        Assert.Equal(note.Id, hits[0].EntityId);
    }

    [Fact]
    public void Catalog_registers_notes_and_todo()
    {
        Assert.Equal([KnownModules.Notes, KnownModules.Todo], KnownModules.All.Select(module => module.Id).ToArray());
    }

    [Fact]
    public void Sqlite_version_can_be_read()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        var version = workspace.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT sqlite_version();";
            return (string)command.ExecuteScalar()!;
        });
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Console.WriteLine("SQLITE " + version);
    }

    private static string ReadAllFiles(string root)
    {
        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            builder.Append(Encoding.UTF8.GetString(File.ReadAllBytes(file)));
        }

        return builder.ToString();
    }
}
