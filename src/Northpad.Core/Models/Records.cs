namespace Northpad.Core.Models;

public enum TaskPriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

public sealed record NoteRecord(
    Guid Id,
    string Title,
    string Body,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record TaskRecord(
    Guid Id,
    string Title,
    string Details,
    DateOnly? DueDate,
    TaskPriority Priority,
    bool IsCompleted,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record ModuleDescriptor(string Id, string Name, string Summary);

public sealed record SearchHit(string ModuleId, Guid EntityId, string Title, string Excerpt);
