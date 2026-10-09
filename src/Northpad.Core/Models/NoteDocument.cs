using System.Text.Json;
using System.Text.Json.Serialization;

namespace Northpad.Core.Models;

public sealed class NoteBlock
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Type { get; set; } = NoteBlockTypes.Paragraph;

    public string Text { get; set; } = string.Empty;

    public bool Done { get; set; }

    public string? FileId { get; set; }
}

public static class NoteBlockTypes
{
    public const string Paragraph = "paragraph";
    public const string Heading = "heading";
    public const string Bullet = "bullet";
    public const string Todo = "todo";
    public const string Quote = "quote";
    public const string Code = "code";
    public const string Divider = "divider";
    public const string Image = "image";

    public static readonly string[] All =
    [
        Paragraph, Heading, Bullet, Todo, Quote, Code, Divider, Image,
    ];

    public static bool IsKnown(string? type) => All.Contains(type, StringComparer.Ordinal);
}

public static class NoteDocument
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IReadOnlyList<NoteBlock> blocks)
    {
        var document = new Document
        {
            Blocks = blocks.Select(block => new NoteBlock
            {
                Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id,
                Type = NoteBlockTypes.IsKnown(block.Type) ? block.Type : NoteBlockTypes.Paragraph,
                Text = block.Text ?? string.Empty,
                Done = block.Done,
                FileId = string.IsNullOrWhiteSpace(block.FileId) ? null : block.FileId,
            }).ToList(),
        };
        return JsonSerializer.Serialize(document, Options);
    }

    public static IReadOnlyList<NoteBlock> Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [new NoteBlock()];
        }

        var trimmed = body.TrimStart();
        if (!trimmed.StartsWith("{\"v\":1", StringComparison.Ordinal))
        {
            return [new NoteBlock { Text = body }];
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(body, Options);
            if (document?.Blocks is not { Count: > 0 })
            {
                return [new NoteBlock { Text = body }];
            }

            return document.Blocks.Select(block => new NoteBlock
            {
                Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id,
                Type = NoteBlockTypes.IsKnown(block.Type) ? block.Type : NoteBlockTypes.Paragraph,
                Text = block.Text ?? string.Empty,
                Done = block.Done,
                FileId = block.FileId,
            }).ToArray();
        }
        catch (JsonException)
        {
            return [new NoteBlock { Text = body }];
        }
    }

    public static string PlainText(string? body)
    {
        var blocks = Parse(body);
        if (blocks.Count == 1 && !bodyLooksLikeDocument(body))
        {
            return blocks[0].Text;
        }

        return string.Join(
            "\n",
            blocks.Select(block => block.Type switch
            {
                NoteBlockTypes.Divider => string.Empty,
                NoteBlockTypes.Image => "image",
                NoteBlockTypes.Todo => (block.Done ? "[done] " : "[ ] ") + block.Text,
                _ => block.Text,
            }));
    }

    public static IEnumerable<Guid> FileIds(string? body)
    {
        foreach (var block in Parse(body))
        {
            if (block.Type == NoteBlockTypes.Image && Guid.TryParse(block.FileId, out var id))
            {
                yield return id;
            }
        }
    }

    private static bool bodyLooksLikeDocument(string? body) =>
        body?.TrimStart().StartsWith("{\"v\":1", StringComparison.Ordinal) == true;

    private sealed class Document
    {
        public int V { get; set; } = 1;

        public List<NoteBlock> Blocks { get; set; } = [];
    }
}
