using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.App.Shell;
using Northpad.Core.Modules;
using Northpad.Core.Search;
using Northpad.Core.Storage;

namespace Northpad.App.Views;

public partial class SearchViewModel : ObservableObject
{
    private readonly INoteRepository _notes;
    private readonly ITaskRepository _tasks;
    private readonly IDocumentRepository _documents;
    private readonly INavigationService _navigation;

    public SearchViewModel(
        INoteRepository notes,
        ITaskRepository tasks,
        IDocumentRepository documents,
        INavigationService navigation)
    {
        _notes = notes;
        _tasks = tasks;
        _documents = documents;
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<SearchRow> _results = [];

    [ObservableProperty]
    private string _status = "Search on this computer. Passwords are not included.";

    partial void OnQueryChanged(string value) => Search();

    [RelayCommand]
    private void Open(SearchRow? row)
    {
        if (row is null)
        {
            return;
        }

        _navigation.OpenModule(row.ModuleId, row.EntityId);
    }

    private void Search()
    {
        var documents = new List<StoredDocument>();
        foreach (var kind in new[]
        {
            DocumentKinds.Calendar,
            DocumentKinds.Reminder,
            DocumentKinds.Mail,
            DocumentKinds.Sheet,
            DocumentKinds.Wallet,
            DocumentKinds.Glossary,
        })
        {
            documents.AddRange(_documents.List(kind));
        }

        var hits = LocalSearch.Find(Query, _notes.List(), _tasks.List(), documents);
        Results = hits.Select(hit => new SearchRow(
            hit.ModuleId,
            hit.EntityId,
            KnownModules.All.FirstOrDefault(module => module.Id == hit.ModuleId)?.Name ?? hit.ModuleId,
            hit.Title,
            hit.Excerpt)).ToArray();
        Status = string.IsNullOrWhiteSpace(Query)
            ? "Search on this computer. Passwords are not included."
            : Results.Count == 0 ? "No matches." : $"{Results.Count} match{(Results.Count == 1 ? "" : "es")}.";
    }
}

public sealed record SearchRow(string ModuleId, Guid EntityId, string ModuleName, string Title, string Excerpt);
