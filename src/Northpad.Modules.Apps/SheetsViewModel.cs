using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public partial class SheetLine : ObservableObject
{
    public SheetLine(IReadOnlyList<string>? cells = null)
    {
        cells ??= [];
        _c0 = Cell(cells, 0);
        _c1 = Cell(cells, 1);
        _c2 = Cell(cells, 2);
        _c3 = Cell(cells, 3);
        _c4 = Cell(cells, 4);
        _c5 = Cell(cells, 5);
        _c6 = Cell(cells, 6);
        _c7 = Cell(cells, 7);
    }

    [ObservableProperty] private string _c0;
    [ObservableProperty] private string _c1;
    [ObservableProperty] private string _c2;
    [ObservableProperty] private string _c3;
    [ObservableProperty] private string _c4;
    [ObservableProperty] private string _c5;
    [ObservableProperty] private string _c6;
    [ObservableProperty] private string _c7;

    public List<string> Values() => [C0, C1, C2, C3, C4, C5, C6, C7];

    private static string Cell(IReadOnlyList<string> cells, int index) =>
        index < cells.Count ? cells[index] : string.Empty;
}

public partial class SheetsViewModel : ObservableObject, IFlushable
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;
    private Guid? _editing;

    public SheetsViewModel(IDocumentRepository documents, IUserConfirmation confirmation)
    {
        _documents = documents;
        _confirmation = confirmation;
        Rows.Add(new SheetLine());
    }

    public ObservableCollection<NamedRow> Items { get; } = [];

    public ObservableCollection<SheetLine> Rows { get; } = [];

    [ObservableProperty]
    private string _name = "Sheet";

    [ObservableProperty]
    private string _status = "Sheets stay on this computer.";

    [ObservableProperty]
    private string _columnTotal = string.Empty;

    public void Initialize(Guid? entityId)
    {
        Reload();
        if (entityId is Guid id)
        {
            var match = Items.FirstOrDefault(item => item.Document.Id == id);
            if (match is not null)
            {
                Edit(match);
            }
        }
    }

    [RelayCommand]
    private void NewItem()
    {
        _editing = null;
        Name = "Sheet";
        Rows.Clear();
        Rows.Add(new SheetLine());
        ColumnTotal = string.Empty;
        Status = "New sheet";
    }

    [RelayCommand]
    private void AddRow()
    {
        if (Rows.Count >= 40)
        {
            Status = "This sheet stops at 40 rows.";
            return;
        }

        Rows.Add(new SheetLine());
    }

    [RelayCommand]
    private void Save()
    {
        var payload = JsonSerializer.Serialize(new SheetFile
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "Sheet" : Name.Trim(),
            Rows = Rows.Select(row => row.Values()).ToList(),
        });
        if (_editing is Guid id)
        {
            _documents.Update(id, payload, null);
        }
        else
        {
            var created = _documents.Create(DocumentKinds.Sheet, payload, null);
            _editing = created.Id;
        }

        UpdateTotal();
        Status = "Saved";
        Reload();
    }

    [RelayCommand]
    private void Delete()
    {
        if (_editing is not Guid id || !_confirmation.Confirm("Delete sheet", "Delete this sheet from this computer?"))
        {
            return;
        }

        _documents.Delete(id);
        NewItem();
        Reload();
    }

    public void Edit(NamedRow row)
    {
        SheetFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SheetFile>(row.Document.Payload);
        }
        catch (JsonException)
        {
            file = null;
        }

        _editing = row.Document.Id;
        Name = file?.Name ?? "Sheet";
        Rows.Clear();
        foreach (var cells in file?.Rows ?? [])
        {
            Rows.Add(new SheetLine(cells));
        }

        if (Rows.Count == 0)
        {
            Rows.Add(new SheetLine());
        }

        UpdateTotal();
        Status = "Saved";
    }

    public void Flush()
    {
        var anyCell = Rows.Any(row => row.Values().Any(cell => !string.IsNullOrWhiteSpace(cell)));
        if (_editing is null && !anyCell)
        {
            return;
        }

        Save();
    }

    private void UpdateTotal()
    {
        decimal total = 0;
        var any = false;
        foreach (var row in Rows)
        {
            if (decimal.TryParse(row.C0, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)
                || decimal.TryParse(row.C0, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                total += value;
                any = true;
            }
        }

        ColumnTotal = any ? "Column A total " + total.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty;
    }

    private void Reload()
    {
        var selected = _editing;
        Items.Clear();
        foreach (var item in _documents.List(DocumentKinds.Sheet))
        {
            string name;
            try
            {
                name = JsonSerializer.Deserialize<SheetFile>(item.Payload)?.Name ?? "Sheet";
            }
            catch (JsonException)
            {
                name = "Sheet";
            }

            Items.Add(new NamedRow(item, name));
        }

        _editing = selected;
    }

    private sealed class SheetFile
    {
        public string Name { get; set; } = "Sheet";

        public List<List<string>> Rows { get; set; } = [];
    }
}
