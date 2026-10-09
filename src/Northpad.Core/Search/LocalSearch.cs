using System.Text.Json;
using Northpad.Core.Models;
using Northpad.Core.Modules;
using Northpad.Core.Storage;

namespace Northpad.Core.Search;

public static class LocalSearch
{
    public static IReadOnlyList<SearchHit> Find(
        string? query,
        IReadOnlyList<NoteRecord> notes,
        IReadOnlyList<TaskRecord> tasks,
        IReadOnlyList<StoredDocument>? documents = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var needle = query.Trim();
        var hits = new List<SearchHit>();
        foreach (var note in notes)
        {
            var plain = NoteDocument.PlainText(note.Body);
            if (Contains(note.Title, needle) || Contains(plain, needle))
            {
                hits.Add(new SearchHit(
                    KnownModules.Notes,
                    note.Id,
                    Display(note.Title, "Untitled note"),
                    Excerpt(plain, needle)));
            }
        }

        foreach (var task in tasks)
        {
            if (Contains(task.Title, needle) || Contains(task.Details, needle))
            {
                hits.Add(new SearchHit(
                    KnownModules.Todo,
                    task.Id,
                    Display(task.Title, "Untitled task"),
                    Excerpt(task.Details, needle)));
            }
        }

        if (documents is not null)
        {
            foreach (var document in documents)
            {
                if (document.Kind == DocumentKinds.Password)
                {
                    continue;
                }

                var text = JsonText.Collect(document.Payload);
                if (!Contains(text, needle))
                {
                    continue;
                }

                hits.Add(new SearchHit(
                    ModuleFor(document.Kind),
                    document.Id,
                    Display(JsonText.First(document.Payload), "Untitled"),
                    Excerpt(text, needle)));
            }
        }

        return hits;
    }

    private static string ModuleFor(string kind) => kind switch
    {
        DocumentKinds.Calendar => KnownModules.Calendar,
        DocumentKinds.Reminder => KnownModules.Reminders,
        DocumentKinds.Mail => KnownModules.Mail,
        DocumentKinds.Sheet => KnownModules.Sheets,
        DocumentKinds.Wallet => KnownModules.Wallet,
        DocumentKinds.Glossary => KnownModules.Translate,
        _ => kind,
    };

    private static bool Contains(string value, string needle) =>
        value.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string Display(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Excerpt(string value, string needle)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = value.ReplaceLineEndings(" ").Trim();
        var index = collapsed.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return collapsed.Length <= 120 ? collapsed : collapsed[..120];
        }

        var start = Math.Max(0, index - 40);
        var length = Math.Min(120, collapsed.Length - start);
        var excerpt = collapsed.Substring(start, length);
        if (start > 0)
        {
            excerpt = "…" + excerpt;
        }

        if (start + length < collapsed.Length)
        {
            excerpt += "…";
        }

        return excerpt;
    }
}
