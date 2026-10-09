using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Northpad.Core;
using Northpad.Core.Models;
using Northpad.Core.Modules;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.Modules.Notes;

public sealed record BlockKind(string Id, string Label);

public partial class BlockRow : ObservableObject
{
    private readonly Action _changed;
    private bool _suppress;

    public BlockRow(NoteBlock block, Action changed)
    {
        _changed = changed;
        Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id;
        FileId = block.FileId;
        _suppress = true;
        _type = NoteBlockTypes.IsKnown(block.Type) ? block.Type : NoteBlockTypes.Paragraph;
        _text = block.Text ?? string.Empty;
        _done = block.Done;
        _suppress = false;
    }

    public string Id { get; }

    public string? FileId { get; set; }

    public IReadOnlyList<BlockKind> Kinds { get; } =
    [
        new(NoteBlockTypes.Paragraph, "Text"),
        new(NoteBlockTypes.Heading, "Heading"),
        new(NoteBlockTypes.Bullet, "Bullet"),
        new(NoteBlockTypes.Todo, "To-do"),
        new(NoteBlockTypes.Quote, "Quote"),
        new(NoteBlockTypes.Code, "Code"),
        new(NoteBlockTypes.Divider, "Divider"),
        new(NoteBlockTypes.Image, "Picture"),
    ];

    [ObservableProperty]
    private string _type;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _done;

    [ObservableProperty]
    private ImageSource? _preview;

    public bool ShowsText => Type is not NoteBlockTypes.Divider and not NoteBlockTypes.Image;

    public bool ShowsDone => Type == NoteBlockTypes.Todo;

    public bool ShowsImage => Type == NoteBlockTypes.Image;

    public NoteBlock ToBlock() => new()
    {
        Id = Id,
        Type = Type,
        Text = Text ?? string.Empty,
        Done = Done,
        FileId = FileId,
    };

    partial void OnTypeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowsText));
        OnPropertyChanged(nameof(ShowsDone));
        OnPropertyChanged(nameof(ShowsImage));
        Changed();
    }

    partial void OnTextChanged(string value) => Changed();

    partial void OnDoneChanged(bool value) => Changed();

    private void Changed()
    {
        if (!_suppress)
        {
            _changed();
        }
    }
}

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
    private readonly IFileStore _files;
    private readonly IFilePicker _picker;
    private readonly IUserConfirmation _confirmation;
    private readonly ILogger<NotesViewModel> _logger;
    private NoteListItem? _loaded;
    private bool _suppress;
    private CancellationTokenSource? _pending;
    private int _generation;
    private bool _disposed;

    public NotesViewModel(
        INoteRepository notes,
        IFileStore files,
        IFilePicker picker,
        IUserConfirmation confirmation,
        ILogger<NotesViewModel> logger)
    {
        _notes = notes;
        _files = files;
        _picker = picker;
        _confirmation = confirmation;
        _logger = logger;
    }

    public ObservableCollection<NoteListItem> Notes { get; } = [];

    public ObservableCollection<NoteListItem> FilteredNotes { get; } = [];

    public ObservableCollection<BlockRow> Blocks { get; } = [];

    [ObservableProperty]
    private NoteListItem? _selectedNote;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

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
    private void AddBlock()
    {
        if (_loaded is null)
        {
            return;
        }

        Blocks.Add(new BlockRow(new NoteBlock(), ScheduleSave));
        ScheduleSave();
    }

    [RelayCommand]
    private void AddPicture()
    {
        if (_loaded is null)
        {
            return;
        }

        var path = _picker.PickOpen("Add a picture", "Pictures|*.png;*.jpg;*.jpeg;*.gif;*.bmp");
        if (path is null)
        {
            return;
        }

        try
        {
            var stored = _files.Import(path, FileKinds.NoteImage);
            var row = new BlockRow(new NoteBlock
            {
                Type = NoteBlockTypes.Image,
                Text = stored.Name,
                FileId = stored.Id.ToString("D"),
            }, ScheduleSave);
            row.Preview = LoadPreview(stored.Id);
            Blocks.Add(row);
            ScheduleSave();
            Status = "Picture added";
        }
        catch (Exception exception) when (exception is StorageException or VaultLockedException or IOException)
        {
            _logger.LogError("Picture import failed. exceptionType={ExceptionType}", exception.GetType().Name);
            Error = exception is VaultLockedException
                ? "Unlock Northpad before adding a picture."
                : "The picture could not be added.";
        }
    }

    [RelayCommand]
    private void DeleteBlock(BlockRow? row)
    {
        if (row is null || _loaded is null)
        {
            return;
        }

        if (Guid.TryParse(row.FileId, out var fileId))
        {
            try
            {
                _files.Delete(fileId);
            }
            catch (Exception exception) when (exception is StorageException or VaultLockedException)
            {
                _logger.LogError("Picture delete failed. exceptionType={ExceptionType}", exception.GetType().Name);
            }
        }

        Blocks.Remove(row);
        if (Blocks.Count == 0)
        {
            Blocks.Add(new BlockRow(new NoteBlock(), ScheduleSave));
        }

        ScheduleSave();
    }

    [RelayCommand]
    private void MoveBlockUp(BlockRow? row) => Move(row, -1);

    [RelayCommand]
    private void MoveBlockDown(BlockRow? row) => Move(row, 1);

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
        var body = _loaded.Body;
        _loaded = null;
        CancelPending();
        foreach (var fileId in NoteDocument.FileIds(body))
        {
            try
            {
                _files.Delete(fileId);
            }
            catch (Exception exception) when (exception is StorageException or VaultLockedException)
            {
                _logger.LogError("Note file delete failed. exceptionType={ExceptionType}", exception.GetType().Name);
            }
        }

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

    private void Move(BlockRow? row, int direction)
    {
        if (row is null)
        {
            return;
        }

        var index = Blocks.IndexOf(row);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= Blocks.Count)
        {
            return;
        }

        Blocks.Move(index, target);
        ScheduleSave();
    }

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
            || NoteDocument.PlainText(note.Body).Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void LoadEditor(NoteListItem? item)
    {
        _suppress = true;
        _loaded = item;
        EditorTitle = item is null ? string.Empty : item.Title == "Untitled" ? string.Empty : item.Title;
        Blocks.Clear();
        if (item is not null)
        {
            foreach (var block in NoteDocument.Parse(item.Body))
            {
                var row = new BlockRow(block, ScheduleSave);
                if (block.Type == NoteBlockTypes.Image && Guid.TryParse(block.FileId, out var fileId))
                {
                    row.Preview = LoadPreview(fileId);
                }

                Blocks.Add(row);
            }
        }

        Status = item is null ? string.Empty : "Saved";
        Error = string.Empty;
        _suppress = false;
    }

    private ImageSource? LoadPreview(Guid fileId)
    {
        try
        {
            var bytes = _files.ReadBytes(fileId);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 720;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is StorageException or VaultLockedException or NotSupportedException or IOException)
        {
            _logger.LogError("Picture preview failed. exceptionType={ExceptionType}", exception.GetType().Name);
            return null;
        }
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
            var body = NoteDocument.Serialize(Blocks.Select(block => block.ToBlock()).ToArray());
            var updated = _notes.Update(_loaded.Id, EditorTitle ?? string.Empty, body);
            _loaded.Title = string.IsNullOrWhiteSpace(EditorTitle) ? "Untitled" : EditorTitle.Trim();
            _loaded.Body = body;
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
