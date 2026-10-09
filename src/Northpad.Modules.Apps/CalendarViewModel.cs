using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.Modules.Apps;

public sealed class EventRow
{
    public EventRow(StoredDocument document, string label)
    {
        Document = document;
        Label = label;
    }

    public StoredDocument Document { get; }

    public string Label { get; }
}

public sealed partial class DayCell : ObservableObject
{
    public DayCell(DateOnly date, bool inMonth)
    {
        Date = date;
        InMonth = inMonth;
        Label = date.Day.ToString(CultureInfo.CurrentCulture);
    }

    public DateOnly Date { get; }

    public bool InMonth { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _selected;

    [ObservableProperty]
    private bool _hasEvents;
}

public partial class CalendarViewModel : ObservableObject, IDisposable, IFlushable
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;
    private DateOnly _month;
    private Guid? _editing;

    public CalendarViewModel(IDocumentRepository documents, IUserConfirmation confirmation)
    {
        _documents = documents;
        _confirmation = confirmation;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _month = new DateOnly(today.Year, today.Month, 1);
        SelectedDate = today;
    }

    public ObservableCollection<DayCell> Days { get; } = [];

    public ObservableCollection<EventRow> Events { get; } = [];

    public string Weekdays { get; } = string.Join("   ", DayNames());

    [ObservableProperty]
    private string _monthLabel = string.Empty;

    [ObservableProperty]
    private DateOnly _selectedDate;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _details = string.Empty;

    [ObservableProperty]
    private string _status = "Events stay on this computer.";

    public void Initialize(Guid? entityId)
    {
        if (entityId is Guid id)
        {
            var match = _documents.List(DocumentKinds.Calendar).FirstOrDefault(item => item.Id == id);
            if (match?.SortKey is { Length: >= 7 } key && DateOnly.TryParse(key, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                _month = new DateOnly(date.Year, date.Month, 1);
                SelectedDate = date;
                Edit(match);
            }
        }

        Rebuild();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        _month = _month.AddMonths(-1);
        Rebuild();
    }

    [RelayCommand]
    private void NextMonth()
    {
        _month = _month.AddMonths(1);
        Rebuild();
    }

    [RelayCommand]
    private void SelectDay(DayCell? day)
    {
        if (day is null)
        {
            return;
        }

        SelectedDate = day.Date;
        if (!day.InMonth)
        {
            _month = new DateOnly(day.Date.Year, day.Date.Month, 1);
        }

        ClearEditor();
        Rebuild();
    }

    [RelayCommand]
    private void NewEvent() => ClearEditor();

    [RelayCommand]
    private void SaveEvent()
    {
        if (!string.IsNullOrWhiteSpace(Time) && !TimeOnly.TryParseExact(Time.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            Status = "Use a time like 09:30, or leave it empty.";
            return;
        }

        var payload = DocJson.Write(new Dictionary<string, string>
        {
            ["title"] = Title.Trim(),
            ["time"] = Time.Trim(),
            ["details"] = Details ?? string.Empty,
        });
        var sort = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        try
        {
            if (_editing is Guid id)
            {
                _documents.Update(id, payload, sort);
            }
            else
            {
                var created = _documents.Create(DocumentKinds.Calendar, payload, sort);
                _editing = created.Id;
            }

            Status = "Saved";
            Rebuild();
        }
        catch (Exception exception) when (exception is StorageException or VaultLockedException)
        {
            Status = "The event could not be saved.";
        }
    }

    [RelayCommand]
    private void DeleteEvent()
    {
        if (_editing is not Guid id)
        {
            return;
        }

        if (!_confirmation.Confirm("Delete event", "Delete this event from this computer?"))
        {
            return;
        }

        _documents.Delete(id);
        ClearEditor();
        Rebuild();
        Status = "Deleted";
    }

    public void Edit(StoredDocument document)
    {
        var values = DocJson.Read(document.Payload);
        _editing = document.Id;
        Title = values.Get("title");
        Time = values.Get("time");
        Details = values.Get("details");
        if (document.SortKey is not null && DateOnly.TryParse(document.SortKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            SelectedDate = date;
        }

        Status = "Editing";
    }

    public void Flush()
    {
        if (_editing is not null || !string.IsNullOrWhiteSpace(Title))
        {
            SaveEvent();
        }
    }

    public void Dispose()
    {
    }

    private void Rebuild()
    {
        MonthLabel = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        var all = _documents.List(DocumentKinds.Calendar);
        var marked = all.Select(item => item.SortKey).Where(key => key is not null).ToHashSet(StringComparer.Ordinal);
        Days.Clear();
        var first = _month;
        var offset = ((int)first.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7;
        var start = first.AddDays(-offset);
        for (var index = 0; index < 42; index++)
        {
            var date = start.AddDays(index);
            var cell = new DayCell(date, date.Month == _month.Month)
            {
                Selected = date == SelectedDate,
                HasEvents = marked.Contains(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            };
            Days.Add(cell);
        }

        Events.Clear();
        var key = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var item in all.Where(item => item.SortKey == key).OrderBy(item => DocJson.Read(item.Payload).Get("time"), StringComparer.Ordinal))
        {
            var values = DocJson.Read(item.Payload);
            var label = string.IsNullOrWhiteSpace(values.Get("time"))
                ? values.Get("title")
                : values.Get("time") + "  " + values.Get("title");
            Events.Add(new EventRow(item, string.IsNullOrWhiteSpace(label) ? "Untitled" : label));
        }
    }

    private void ClearEditor()
    {
        _editing = null;
        Title = string.Empty;
        Time = string.Empty;
        Details = string.Empty;
    }

    private static IEnumerable<string> DayNames()
    {
        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        var start = (int)format.FirstDayOfWeek;
        for (var index = 0; index < 7; index++)
        {
            yield return format.AbbreviatedDayNames[(start + index) % 7];
        }
    }
}
