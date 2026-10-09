using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public sealed class ReminderRow
{
    public ReminderRow(StoredDocument document)
    {
        Document = document;
        var values = DocJson.Read(document.Payload);
        Title = string.IsNullOrWhiteSpace(values.Get("title")) ? "Reminder" : values.Get("title");
        Due = values.Get("due").Replace('T', ' ');
        Done = values.Get("done") == "true";
    }

    public StoredDocument Document { get; }

    public string Title { get; }

    public string Due { get; }

    public bool Done { get; }
}

public partial class RemindersViewModel : ObservableObject
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;

    public RemindersViewModel(IDocumentRepository documents, IUserConfirmation confirmation)
    {
        _documents = documents;
        _confirmation = confirmation;
    }

    public ObservableCollection<ReminderRow> Items { get; } = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _due = string.Empty;

    [ObservableProperty]
    private string _status = "Reminders appear while Northpad is open. Closing the app pauses them.";

    public void Initialize(Guid? entityId) => Reload();

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            Status = "Add a title.";
            return;
        }

        if (!DateTime.TryParseExact(Due.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due))
        {
            Status = "Use a due time like 2026-10-09 15:30.";
            return;
        }

        var payload = DocJson.Write(new Dictionary<string, string>
        {
            ["title"] = Title.Trim(),
            ["due"] = due.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ["done"] = "false",
            ["notified"] = "false",
        });
        _documents.Create(DocumentKinds.Reminder, payload, due.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
        Title = string.Empty;
        Due = string.Empty;
        Status = "Saved";
        Reload();
    }

    [RelayCommand]
    private void Toggle(ReminderRow? row)
    {
        if (row is null)
        {
            return;
        }

        var values = DocJson.Read(row.Document.Payload);
        values["done"] = values.Get("done") == "true" ? "false" : "true";
        _documents.Update(row.Document.Id, DocJson.Write(values), row.Document.SortKey);
        Reload();
    }

    [RelayCommand]
    private void Delete(ReminderRow? row)
    {
        if (row is null || !_confirmation.Confirm("Delete reminder", "Delete this reminder from this computer?"))
        {
            return;
        }

        _documents.Delete(row.Document.Id);
        Reload();
    }

    private void Reload()
    {
        Items.Clear();
        foreach (var item in _documents.List(DocumentKinds.Reminder).OrderBy(item => item.SortKey, StringComparer.Ordinal))
        {
            Items.Add(new ReminderRow(item));
        }
    }
}
