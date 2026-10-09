using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Northpad.Core;
using Northpad.Core.Models;
using Northpad.Core.Storage;

namespace Northpad.Modules.Todo;

public partial class TaskListItem : ObservableObject
{
    public TaskListItem(TaskRecord record)
    {
        Id = record.Id;
        Details = record.Details;
        DueDate = record.DueDate;
        Priority = record.Priority;
        CreatedUtc = record.CreatedUtc;
        _title = string.IsNullOrWhiteSpace(record.Title) ? "Untitled" : record.Title.Trim();
        _isCompleted = record.IsCompleted;
        _meta = FormatMeta(record.DueDate, record.Priority);
    }

    public Guid Id { get; }

    public string Details { get; set; }

    public DateOnly? DueDate { get; set; }

    public TaskPriority Priority { get; set; }

    public DateTimeOffset CreatedUtc { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _meta;

    public static string FormatMeta(DateOnly? dueDate, TaskPriority priority)
    {
        var parts = new List<string>();
        if (dueDate is DateOnly due)
        {
            parts.Add(due.ToString("d MMM yyyy", CultureInfo.CurrentCulture));
        }

        if (priority != TaskPriority.None)
        {
            parts.Add(priority.ToString());
        }

        return string.Join(" · ", parts);
    }
}

public partial class TodoViewModel : ObservableObject, IFlushable, IDisposable
{
    private readonly ITaskRepository _tasks;
    private readonly IUserConfirmation _confirmation;
    private readonly ILogger<TodoViewModel> _logger;
    private TaskListItem? _loaded;
    private bool _suppress;
    private CancellationTokenSource? _pending;
    private int _generation;

    public TodoViewModel(ITaskRepository tasks, IUserConfirmation confirmation, ILogger<TodoViewModel> logger)
    {
        _tasks = tasks;
        _confirmation = confirmation;
        _logger = logger;
    }

    public ObservableCollection<TaskListItem> Tasks { get; } = [];

    public ObservableCollection<TaskListItem> FilteredTasks { get; } = [];

    public IReadOnlyList<TaskPriority> Priorities { get; } = Enum.GetValues<TaskPriority>();

    [ObservableProperty]
    private TaskListItem? _selectedTask;

    [ObservableProperty]
    private string _composer = string.Empty;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorDetails = string.Empty;

    [ObservableProperty]
    private string _dueText = string.Empty;

    [ObservableProperty]
    private TaskPriority _editorPriority = TaskPriority.None;

    [ObservableProperty]
    private bool _editorCompleted;

    [ObservableProperty]
    private string _filterMode = "open";

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private string _emptyMessage = "No open tasks.";

    public void Initialize(Guid? selectId)
    {
        Reload();
        var match = Tasks.FirstOrDefault(task => task.Id == selectId) ?? FilteredTasks.FirstOrDefault();
        if (match is not null)
        {
            Select(match);
        }
    }

    [RelayCommand]
    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(Composer))
        {
            Error = "Add a task title.";
            return;
        }

        Flush();
        var created = _tasks.Create(Composer.Trim(), string.Empty, null, TaskPriority.None);
        Composer = string.Empty;
        var item = new TaskListItem(created);
        Tasks.Insert(0, item);
        ApplyFilter();
        Select(item);
        Error = string.Empty;
    }

    [RelayCommand]
    private void DeleteTask()
    {
        if (_loaded is null)
        {
            return;
        }

        if (!_confirmation.Confirm("Delete task", "Delete this task from this computer?"))
        {
            return;
        }

        var id = _loaded.Id;
        _loaded = null;
        CancelPending();
        _tasks.Delete(id);
        var existing = Tasks.FirstOrDefault(task => task.Id == id);
        if (existing is not null)
        {
            Tasks.Remove(existing);
        }

        ApplyFilter();
        var next = FilteredTasks.FirstOrDefault();
        if (next is null)
        {
            SelectedTask = null;
            LoadEditor(null);
            return;
        }

        Select(next);
    }

    [RelayCommand]
    private void ShowOpen() => SetFilter("open");

    [RelayCommand]
    private void ShowDone() => SetFilter("done");

    [RelayCommand]
    private void ShowAll() => SetFilter("all");

    public void Select(TaskListItem item)
    {
        if (ReferenceEquals(item, _loaded))
        {
            SelectedTask = item;
            return;
        }

        Flush();
        SelectedTask = item;
        LoadEditor(item);
    }

    public void Flush()
    {
        CancelPending();
        _generation++;
        SaveLoaded();
    }

    public void Dispose()
    {
        Flush();
        CancelPending();
    }

    partial void OnEditorTitleChanged(string value) => ScheduleSave();

    partial void OnEditorDetailsChanged(string value) => ScheduleSave();

    partial void OnDueTextChanged(string value) => ScheduleSave();

    partial void OnEditorPriorityChanged(TaskPriority value) => ScheduleSave();

    partial void OnEditorCompletedChanged(bool value) => ScheduleSave();

    private void SetFilter(string mode)
    {
        FilterMode = mode;
        ApplyFilter();
    }

    private void Reload()
    {
        Tasks.Clear();
        foreach (var task in _tasks.List())
        {
            Tasks.Add(new TaskListItem(task));
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredTasks.Clear();
        foreach (var task in Tasks.Where(Matches))
        {
            FilteredTasks.Add(task);
        }

        ShowEmpty = FilteredTasks.Count == 0;
        EmptyMessage = FilterMode switch
        {
            "done" => "No completed tasks.",
            "all" => "No tasks yet.",
            _ => "No open tasks.",
        };
    }

    private bool Matches(TaskListItem task) => FilterMode switch
    {
        "done" => task.IsCompleted,
        "all" => true,
        _ => !task.IsCompleted,
    };

    private void LoadEditor(TaskListItem? item)
    {
        _suppress = true;
        _loaded = item;
        EditorTitle = item is null ? string.Empty : item.Title == "Untitled" ? string.Empty : item.Title;
        EditorDetails = item?.Details ?? string.Empty;
        DueText = item?.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        EditorPriority = item?.Priority ?? TaskPriority.None;
        EditorCompleted = item?.IsCompleted ?? false;
        Status = item is null ? string.Empty : "Saved";
        Error = string.Empty;
        _suppress = false;
    }

    private void ScheduleSave()
    {
        if (_suppress || _loaded is null)
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
            await Task.Delay(300, token);
            if (generation != _generation)
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

        if (!TryParseDue(out var due))
        {
            Error = "Use a due date like 2026-10-09, or leave it empty.";
            Status = "Not saved";
            return;
        }

        try
        {
            var record = new TaskRecord(
                _loaded.Id,
                EditorTitle ?? string.Empty,
                EditorDetails ?? string.Empty,
                due,
                EditorPriority,
                EditorCompleted,
                _loaded.CreatedUtc,
                DateTimeOffset.UtcNow);
            var updated = _tasks.Update(record);
            _loaded.Title = string.IsNullOrWhiteSpace(updated.Title) ? "Untitled" : updated.Title.Trim();
            _loaded.Details = updated.Details;
            _loaded.DueDate = updated.DueDate;
            _loaded.Priority = updated.Priority;
            _loaded.IsCompleted = updated.IsCompleted;
            _loaded.Meta = TaskListItem.FormatMeta(updated.DueDate, updated.Priority);
            Status = "Saved";
            Error = string.Empty;
            ApplyFilter();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Task save failed.");
            Status = "Could not save";
            Error = "The task could not be saved. It is still open in the editor.";
        }
    }

    private bool TryParseDue(out DateOnly? due)
    {
        if (string.IsNullOrWhiteSpace(DueText))
        {
            due = null;
            return true;
        }

        if (DateOnly.TryParseExact(DueText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            due = parsed;
            return true;
        }

        due = null;
        return false;
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
