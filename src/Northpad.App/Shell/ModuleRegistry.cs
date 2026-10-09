using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Northpad.Core.Modules;
using Northpad.Modules.Apps;
using Northpad.Modules.Notes;
using Northpad.Modules.Todo;

namespace Northpad.App.Shell;

public static class ModuleRegistry
{
    public static IReadOnlyList<string> ViewIds { get; } =
    [
        KnownModules.Notes,
        KnownModules.Todo,
        KnownModules.Calendar,
        KnownModules.Reminders,
        KnownModules.Mail,
        KnownModules.Browser,
        KnownModules.Photos,
        KnownModules.Maps,
        KnownModules.WebSearch,
        KnownModules.Video,
        KnownModules.Passwords,
        KnownModules.Drive,
        KnownModules.Sheets,
        KnownModules.Translate,
        KnownModules.Wallet,
    ];

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
            KnownModules.Calendar => Host(services.GetRequiredService<CalendarView>(), services.GetRequiredService<CalendarViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Reminders => Host(services.GetRequiredService<RemindersView>(), services.GetRequiredService<RemindersViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Mail => Host(services.GetRequiredService<MailView>(), services.GetRequiredService<MailViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Browser => Host(services.GetRequiredService<BrowserView>(), services.GetRequiredService<BrowserViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Photos => Host(services.GetRequiredService<LibraryView>(), services.GetRequiredService<PhotosViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Drive => Host(services.GetRequiredService<LibraryView>(), services.GetRequiredService<DriveViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Maps => Host(services.GetRequiredService<MapsView>(), services.GetRequiredService<MapsViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.WebSearch => Host(services.GetRequiredService<WebSearchView>(), services.GetRequiredService<WebSearchViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Video => Host(services.GetRequiredService<VideoView>(), services.GetRequiredService<VideoViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Passwords => Host(services.GetRequiredService<PasswordsView>(), services.GetRequiredService<PasswordsViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Sheets => Host(services.GetRequiredService<SheetsView>(), services.GetRequiredService<SheetsViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Translate => Host(services.GetRequiredService<TranslateView>(), services.GetRequiredService<TranslateViewModel>(), viewModel => viewModel.Initialize(entityId)),
            KnownModules.Wallet => Host(services.GetRequiredService<WalletView>(), services.GetRequiredService<WalletViewModel>(), viewModel => viewModel.Initialize(entityId)),
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
