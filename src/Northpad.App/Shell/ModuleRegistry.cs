using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Northpad.Core.Modules;
using Northpad.Modules.Notes;
using Northpad.Modules.Todo;

namespace Northpad.App.Shell;

public static class ModuleRegistry
{
    public static IReadOnlyList<string> ViewIds { get; } = [KnownModules.Notes, KnownModules.Todo];

    public static void EnsureMatchesCatalog()
    {
        var catalog = KnownModules.All.Select(module => module.Id).Order(StringComparer.Ordinal);
        var views = ViewIds.Order(StringComparer.Ordinal);
        if (!catalog.SequenceEqual(views, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("A module is missing its workspace view.");
        }
    }

    public static FrameworkElement Create(IServiceProvider services, string moduleId, Guid? entityId)
    {
        return moduleId switch
        {
            KnownModules.Notes => Host(
                services.GetRequiredService<NotesView>(),
                services.GetRequiredService<NotesViewModel>(),
                viewModel => viewModel.Initialize(entityId)),
            KnownModules.Todo => Host(
                services.GetRequiredService<TodoView>(),
                services.GetRequiredService<TodoViewModel>(),
                viewModel => viewModel.Initialize(entityId)),
            _ => throw new InvalidOperationException("Unknown module."),
        };
    }

    private static FrameworkElement Host<TView, TViewModel>(TView view, TViewModel viewModel, Action<TViewModel> initialize)
        where TView : FrameworkElement
    {
        initialize(viewModel);
        view.DataContext = viewModel;
        return view;
    }
}
