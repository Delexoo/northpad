using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Northpad.Core;
using Northpad.Core.Storage;

namespace Northpad.Modules.Notes;

public partial class NoteListItem : ObservableObject
{
    public NoteListItem(Guid id, string title, string body, DateTimeOffset updatedUtc)
    {
        Id = id;
        Body = body;
        UpdatedUtc = updatedUtc;
        _title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        _updatedLabel = Format(updatedUtc);
    }

    public Guid Id { get; }

    public string Body { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _updatedLabel;

    public static string Format(DateTimeOffset utc)
    {
        var local = utc.ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("t", CultureInfo.CurrentCulture)
            : local.ToString("d MMM", CultureInfo.CurrentCulture);
    }
}

public partial class NotesViewModel : ObservableObject, IFlushable, IDisposable
{
    private readonly INoteRepository _notes;
    private readonly IUserConfirmation _confirmation;
    private readonly ILogger<NotesViewModel> _logger;
    private NoteListItem? _loaded;
    private bool _suppress;
    private CancellationTokenSource? _pending;
    private int _generation;
    private bool _disposed;

    public NotesViewModel(INoteRepository notes, IUserConfirmation confirmation, ILogger<NotesViewModel> logger)
    {
        _notes = notes;
        _confirmation = confirmation;
        _logger = logger;
    }

    public ObservableCollection<NoteListItem> Notes { get; } = [];

    public ObservableCollection<NoteListItem> FilteredNotes { get; } = [];

    [ObservableProperty]
    private NoteListItem? _selectedNote;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorBody = string.Empty;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private string _emptyMessage = "No notes yet.";

    public void Initialize(Guid? selectId)
    {
        Reload();
        var match = Notes.FirstOrDefault(note => note.Id == selectId) ?? FilteredNotes.FirstOrDefault();
        if (match is not null)
        {
            Select(match);
        }
    }

    [RelayCommand]
    private void NewNote()
    {
        Flush();
        var created = _notes.Create(string.Empty, string.Empty);
        var item = new NoteListItem(created.Id, created.Title, created.Body, created.UpdatedUtc);
        Notes.Insert(0, item);
        ApplyFilter();
        Select(item);
    }

    [RelayCommand]
    private void DeleteNote()
    {
        if (_loaded is null)
        {
            return;
        }

        if (!_confirmation.Confirm("Delete note", "Delete this note from this computer?"))
        {
            return;
        }

        var id = _loaded.Id;
        _loaded = null;
        CancelPending();
        _notes.Delete(id);
        var existing = Notes.FirstOrDefault(note => note.Id == id);
        if (existing is not null)
        {
            Notes.Remove(existing);
        }

        ApplyFilter();
        var next = FilteredNotes.FirstOrDefault();
        if (next is null)
        {
            SelectedNote = null;
            LoadEditor(null);
            return;
        }

        Select(next);
    }

    public void Select(NoteListItem item)
    {
        if (ReferenceEquals(item, _loaded))
        {
            SelectedNote = item;
            return;
        }

        Flush();
        SelectedNote = item;
        LoadEditor(item);
    }

    public void Flush()
    {
        if (_suppress || _disposed)
        {
            return;
        }

        CancelPending();
        _generation++;
        SaveLoaded();
    }

    public void Dispose()
    {
        Flush();
        _disposed = true;
        CancelPending();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnEditorTitleChanged(string value) => ScheduleSave();

    partial void OnEditorBodyChanged(string value) => ScheduleSave();

    private void Reload()
    {
        Notes.Clear();
        foreach (var note in _notes.List())
        {
            Notes.Add(new NoteListItem(note.Id, note.Title, note.Body, note.UpdatedUtc));
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredNotes.Clear();
        foreach (var note in Notes.Where(Matches))
        {
            FilteredNotes.Add(note);
        }

        ShowEmpty = FilteredNotes.Count == 0;
        EmptyMessage = Notes.Count == 0 ? "No notes yet." : "No matching notes.";
    }

    private bool Matches(NoteListItem note)
    {
        if (string.IsNullOrWhiteSpace(Filter))
        {
            return true;
        }

        return note.Title.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase)
            || note.Body.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void LoadEditor(NoteListItem? item)
    {
        _suppress = true;
        _loaded = item;
        EditorTitle = item is null ? string.Empty : item.Title == "Untitled" ? string.Empty : item.Title;
        EditorBody = item?.Body ?? string.Empty;
        Status = item is null ? string.Empty : "Saved";
        Error = string.Empty;
        _suppress = false;
    }

    private void ScheduleSave()
    {
        if (_suppress || _loaded is null || _disposed)
        {
            return;
        }

        Status = "Saving";
        CancelPending();
        var generation = ++_generation;
        var cts = new CancellationTokenSource();
        _pending = cts;
        _ = SaveLaterAsync(generation, cts.Token);
    }

    private async Task SaveLaterAsync(int generation, CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
            if (generation != _generation || _disposed)
            {
                return;
            }

            SaveLoaded();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SaveLoaded()
    {
        if (_loaded is null)
        {
            return;
        }

        try
        {
            var updated = _notes.Update(_loaded.Id, EditorTitle ?? string.Empty, EditorBody ?? string.Empty);
            _loaded.Title = string.IsNullOrWhiteSpace(EditorTitle) ? "Untitled" : EditorTitle.Trim();
            _loaded.Body = EditorBody ?? string.Empty;
            _loaded.UpdatedUtc = updated.UpdatedUtc;
            _loaded.UpdatedLabel = NoteListItem.Format(updated.UpdatedUtc);
            Status = "Saved";
            Error = string.Empty;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Note save failed.");
            Status = "Could not save";
            Error = "The note could not be saved. It is still open in the editor.";
        }
    }

    private void CancelPending()
    {
        if (_pending is null)
        {
            return;
        }

        _pending.Cancel();
        _pending.Dispose();
        _pending = null;
    }
}
