using System.Text;
using Northpad.Core.Maps;
using Northpad.Core.Models;
using Northpad.Core.Modules;
using Northpad.Core.Storage;
using Northpad.Core.Translate;

namespace Northpad.Core.Tests;

public class ToolStorageTests
{
    [Fact]
    public void Plain_notes_stay_readable_as_one_block()
    {
        var blocks = NoteDocument.Parse("Buy oats");
        Assert.Single(blocks);
        Assert.Equal("Buy oats", blocks[0].Text);
        Assert.Equal("Buy oats", NoteDocument.PlainText("Buy oats"));
    }

    [Fact]
    public void Block_notes_roundtrip_and_keep_picture_ids()
    {
        var id = Guid.NewGuid();
        var body = NoteDocument.Serialize(
        [
            new NoteBlock { Type = NoteBlockTypes.Heading, Text = "Plan" },
            new NoteBlock { Type = NoteBlockTypes.Todo, Text = "Pack", Done = true },
            new NoteBlock { Type = NoteBlockTypes.Image, FileId = id.ToString("D") },
        ]);
        var blocks = NoteDocument.Parse(body);
        Assert.Equal(3, blocks.Count);
        Assert.Equal(NoteBlockTypes.Heading, blocks[0].Type);
        Assert.True(blocks[1].Done);
        Assert.Equal([id], NoteDocument.FileIds(body).ToArray());
        Assert.Contains("Pack", NoteDocument.PlainText(body), StringComparison.Ordinal);
    }

    [Fact]
    public void Documents_and_files_are_not_plaintext()
    {
        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var documents = new DocumentRepository(workspace.Database, workspace.Vault.Protector);
        var files = new EncryptedFileStore(workspace.Database, workspace.Vault.Protector, workspace.Paths);
        const string secret = "VaultPhrase-QQ-91";
        documents.Create(DocumentKinds.Password, "{\"name\":\"bank\",\"secret\":\"" + secret + "\"}", null);
        var source = Path.Combine(workspace.Root, "plain.txt");
        Directory.CreateDirectory(workspace.Root);
        File.WriteAllText(source, secret);
        var stored = files.Import(source, FileKinds.Drive);
        Assert.Equal(secret, Encoding.UTF8.GetString(files.ReadBytes(stored.Id)));
        File.Delete(source);
        workspace.Database.Checkpoint();
        workspace.Database.ReleaseConnections();
        var haystack = string.Concat(Directory.EnumerateFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .Select(path => Encoding.UTF8.GetString(File.ReadAllBytes(path))));
        Assert.DoesNotContain(secret, haystack);
    }

    [Fact]
    public void Version_one_database_gains_document_tables()
    {
        using var workspace = new TestWorkspace();
        workspace.Paths.EnsureDirectories();
        workspace.Database.Migrate();
        workspace.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_info SET version = 1 WHERE id = 1; DROP TABLE documents; DROP TABLE files;";
            command.ExecuteNonQuery();
        });
        workspace.Database.Migrate();
        var version = workspace.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT version FROM schema_info WHERE id = 1;";
            return (long)command.ExecuteScalar()!;
        });
        Assert.Equal(2, version);
    }

    [Fact]
    public void Map_tile_for_the_origin_is_stable()
    {
        var tile = SlippyMap.Tile(0, 0, 1);
        Assert.Equal((1, 1), tile);
        Assert.Equal("https://tile.openstreetmap.org/0/0/0.png", SlippyMap.TileUri(0, 0, 0).AbsoluteUri);
    }

    [Fact]
    public void Place_parser_reads_nominatim_json()
    {
        const string json = """[{"lat":"51.5","lon":"-0.12","display_name":"London"}]""";
        Assert.True(NominatimParser.TryRead(json, out var latitude, out var longitude, out var label));
        Assert.Equal(51.5, latitude);
        Assert.Equal(-0.12, longitude);
        Assert.Equal("London", label);
    }

    [Fact]
    public void Translation_parser_and_blocked_hosts()
    {
        Assert.True(LibreTranslate.TryRead("""{"translatedText":"hola"}""", out var text));
        Assert.Equal("hola", text);
        Assert.False(EndpointPolicy.IsAllowedTranslateEndpoint("https://translate.google.com/translate"));
        Assert.True(EndpointPolicy.IsAllowedTranslateEndpoint("http://127.0.0.1:5000"));
        Assert.False(EndpointPolicy.IsAllowedTranslateEndpoint("http://example.com"));
    }
}
