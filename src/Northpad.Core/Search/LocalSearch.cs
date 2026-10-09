using Northpad.Core.Models;
using Northpad.Core.Modules;

namespace Northpad.Core.Search;

public static class LocalSearch
{
    public static IReadOnlyList<SearchHit> Find(
        string? query,
        IReadOnlyList<NoteRecord> notes,
        IReadOnlyList<TaskRecord> tasks)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var needle = query.Trim();
        var hits = new List<SearchHit>();
        foreach (var note in notes)
        {
            if (Contains(note.Title, needle) || Contains(note.Body, needle))
            {
                hits.Add(new SearchHit(
                    KnownModules.Notes,
                    note.Id,
                    Display(note.Title, "Untitled note"),
                    Excerpt(note.Body, needle)));
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

        return hits;
    }

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
