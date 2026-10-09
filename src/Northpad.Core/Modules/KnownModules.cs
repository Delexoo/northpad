using Northpad.Core.Models;

namespace Northpad.Core.Modules;

public static class KnownModules
{
    public const string Notes = "notes";
    public const string Todo = "todo";

    public static IReadOnlyList<ModuleDescriptor> All { get; } =
    [
        new(Notes, "Notes", "Write and search notes on this computer."),
        new(Todo, "Todo", "Keep tasks on this computer."),
    ];
}
